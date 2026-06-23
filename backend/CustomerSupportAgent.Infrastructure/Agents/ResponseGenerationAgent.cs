using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Configuration;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;
using CustomerSupportAgent.Infrastructure.Plugins;

namespace CustomerSupportAgent.Infrastructure.Agents;

public class ResponseGenerationAgent : ISupportAgent
{
    private readonly Kernel _kernel;
    private readonly IConfiguration _configuration;

    public ResponseGenerationAgent(Kernel kernel, IConfiguration configuration)
    {
        _kernel = kernel.Clone();
        _kernel.Plugins.AddFromType<CrmPlugin>();
        _kernel.Plugins.AddFromType<OrderPlugin>();
        _configuration = configuration;
    }

    public string Name => "Response Generation Agent";

    private bool IsDummyKey()
    {
        var key = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return string.IsNullOrWhiteSpace(key) || 
               key.Equals("dummy-key-for-initial-setup", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgentLog> RunAsync(Ticket ticket, Dictionary<string, object>? context = null)
    {
        var log = new AgentLog
        {
            TicketId = ticket.Id,
            AgentName = Name,
            Action = "Drafting Response",
            Input = $"Customer Email: {ticket.CustomerEmail}\nSubject: {ticket.Subject}\nIntent: {ticket.Intent}",
            CreatedAt = DateTime.UtcNow
        };

        if (IsDummyKey())
        {
            // Offline / Mock Simulation Mode
            string customerName = "Valued Customer";
            string? orderId = null;
            
            if (ticket.MetadataJson != null)
            {
                try
                {
                    using var jsonDoc = JsonDocument.Parse(ticket.MetadataJson);
                    var root = jsonDoc.RootElement;
                    if (root.TryGetProperty("customerName", out var nameProp) && nameProp.ValueKind != JsonValueKind.Null)
                        customerName = nameProp.GetString() ?? "Valued Customer";
                    if (root.TryGetProperty("orderId", out var orderProp) && orderProp.ValueKind != JsonValueKind.Null)
                        orderId = orderProp.GetString();
                }
                catch { }
            }

            // Simulate executing native plugins
            var crm = new CrmPlugin();
            var crmResult = crm.GetCustomerProfile(ticket.CustomerEmail);

            string orderResult = "";
            if (!string.IsNullOrEmpty(orderId))
            {
                var order = new OrderPlugin();
                orderResult = order.GetOrderDetails(orderId);
            }

            string draftContent = "";

            if (ticket.Intent == "Refund")
            {
                draftContent = $"""
Dear {customerName},

Thank you for reaching out regarding a refund.

As per our refund policy, purchases are eligible for a full refund within 30 days of the purchase date, provided the items are unused and in original packaging. 

For your order ({orderId ?? "99482"}), it is currently eligible. Return shipping is free since your order total was over $50. Once we receive and inspect the item, the funds will be credited back to your original payment card within 5-10 business days.

Please let us know if you would like us to email you the return shipping label.

Best regards,
Customer Support Team
""";
            }
            else if (ticket.Intent == "Shipping")
            {
                string deliveryInfo = orderId == "10293" 
                    ? "Your order 10293 was shipped via FedEx and is scheduled for delivery on June 26, 2026. Tracking URL: https://fedex-sim.com/track/10293"
                    : "Your order is currently processing in the warehouse and will dispatch shortly.";

                draftContent = $"""
Dear {customerName},

Thank you for contacting customer support.

I would be happy to help you locate your package. {deliveryInfo}

Please note that tracking links can take up to 24 hours after dispatch to show active details. If you have any further questions, please let us know.

Best regards,
Customer Support Team
""";
            }
            else if (ticket.Intent == "TechSupport")
            {
                draftContent = $"""
Dear {customerName},

Thank you for reaching out.

Regarding the login lockout you described, accounts are locked for 15 minutes after 5 unsuccessful attempts. Unfortunately, customer support agents are unable to manually override this lock.

Please clear your browser cache, try using an incognito window, and attempt to log in again once the 15-minute window expires. You can also trigger a password reset using the link on the login page.

Best regards,
Customer Support Team
""";
            }
            else
            {
                draftContent = $"""
Dear {customerName},

Thank you for contacting our support desk.

We have received your email regarding "{ticket.Subject}". I am escalating this to our supervisor queue. A team member will review the case details and follow up with you within 4 hours.

We appreciate your patience.

Best regards,
Customer Support Team
""";
            }

            // Handle revision draft modifications
            if (ticket.Drafts.Count > 0)
            {
                var prevDraft = ticket.Drafts.OrderByDescending(d => d.CreatedAt).First();
                draftContent = $"""
Dear {customerName},

Thank you for your response.

(REVISED DRAFT): 
I have updated the details for order {orderId ?? "99482"} based on your request. 
Feedback Addressed: {prevDraft.OperatorComments ?? prevDraft.ReviewFeedback}

We are processing this update immediately.

Best regards,
Customer Support Team
""";
            }

            log.Input += $"\n[CRM Lookup Result]:\n{crmResult}";
            if (!string.IsNullOrEmpty(orderResult))
            {
                log.Input += $"\n[Order Lookup Result]:\n{orderResult}";
            }

            log.Output = draftContent + "\n\n[OFFLINE SIMULATION LOG]: Auto-called native tools (CRM & Order lookup) and constructed response response locally.";

            var draft = new TicketDraft
            {
                TicketId = ticket.Id,
                Content = draftContent,
                Status = DraftStatus.PendingApproval,
                CreatedAt = DateTime.UtcNow
            };
            
            ticket.Drafts.Add(draft);
            if (context != null)
            {
                context["latest_draft"] = draft;
            }

            log.Status = "Success";
            return log;
        }

        // Retrieve RAG sources from context
        string retrievedDocsContent = "";
        if (context != null && context.TryGetValue("retrieved_docs", out var docsObj) && docsObj is List<KnowledgeDocument> docs)
        {
            retrievedDocsContent = string.Join("\n\n", docs.Select(d => $"[Source: {d.Title}] ({d.Category})\n{d.Content}"));
        }

        string pastDraftsHistory = "";
        if (ticket.Drafts.Count > 0)
        {
            var latestDraft = ticket.Drafts.OrderByDescending(d => d.CreatedAt).First();
            pastDraftsHistory = $"""
                ---
                REVISION NOTE: A draft was previously generated but rejected. 
                Previous Draft Content:
                {latestDraft.Content}

                Quality Review Feedback: {latestDraft.ReviewFeedback}
                Operator Rejection Notes: {latestDraft.OperatorComments}

                Please completely rewrite the response, addressing the feedback above.
                ---
                """;
        }

        var prompt = $$"""
            You are a Response Generation Agent for a customer support team.
            Your job is to draft a professional, helpful email response to a customer email.

            To do your job, you MUST use the tools available (such as looking up CRM customer details or order details) when needed to get accurate, concrete customer facts.
            Always lookup CRM customer details based on customer email first, to address the customer by name and know their tier.
            If the customer is asking about a shipping delay, delivery status, or tracking an order, you MUST lookup order details using the Order ID.
            If you need to cite refund policies, shipping times, or other FAQs, check the "Retrieved Knowledge Base Documents" below. Do not make up policies; only use the facts retrieved.

            Retrieved Knowledge Base Documents:
            ---
            {{(string.IsNullOrEmpty(retrievedDocsContent) ? "No knowledge documents retrieved." : retrievedDocsContent)}}
            ---

            {{pastDraftsHistory}}

            Customer Email details:
            - Email: {{ticket.CustomerEmail}}
            - Subject: {{ticket.Subject}}
            - Body:
            {{ticket.Body}}

            Response Guidelines:
            - Address the customer by name (retrieve via CRM lookup tool).
            - Keep the tone polite, supportive, and brand-aligned.
            - Answer all customer questions thoroughly based on retrieved documents and order details.
            - Provide concrete order status and tracking URLs if looking up shipping or order tracking.
            - Close the email with a professional signature: "Best regards,\nCustomer Support Team".
            - Output ONLY the final draft email content. Do not output any markdown code blocks (e.g. do not wrap the email in ``` or other formatting).

            Email Draft:
            """;

        try
        {
            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var response = await _kernel.InvokePromptAsync<string>(prompt, new KernelArguments(settings));
            var draftContent = response?.Trim() ?? string.Empty;

            log.Output = draftContent;

            var draft = new TicketDraft
            {
                TicketId = ticket.Id,
                Content = draftContent,
                Status = DraftStatus.PendingApproval,
                CreatedAt = DateTime.UtcNow
            };
            
            ticket.Drafts.Add(draft);
            if (context != null)
            {
                context["latest_draft"] = draft;
            }

            log.Status = "Success";
        }
        catch (Exception ex)
        {
            log.Status = "Error";
            log.Output = $"Failed to draft response: {ex.Message}";
        }

        return log;
    }
}
