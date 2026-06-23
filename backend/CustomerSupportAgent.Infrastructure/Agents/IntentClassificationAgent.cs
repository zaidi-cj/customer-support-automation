using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using CustomerSupportAgent.Core.Interfaces;
using CustomerSupportAgent.Core.Models;

namespace CustomerSupportAgent.Infrastructure.Agents;

public class IntentClassificationAgent : ISupportAgent
{
    private readonly Kernel _kernel;
    private readonly IConfiguration _configuration;

    public IntentClassificationAgent(Kernel kernel, IConfiguration configuration)
    {
        _kernel = kernel;
        _configuration = configuration;
    }

    public string Name => "Intent Classification Agent";

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
            Action = "Classifying Intent",
            Input = $"Subject: {ticket.Subject}\nBody: {ticket.Body}",
            CreatedAt = DateTime.UtcNow
        };

        if (IsDummyKey())
        {
            // Offline / Mock Simulation Mode
            string bodyLower = ticket.Body.ToLower();
            string subjectLower = ticket.Subject.ToLower();
            
            string intent = "General";
            string category = "General Inquiry";
            string orderId = "null";
            string customerName = "Valued Customer";

            if (bodyLower.Contains("refund") || bodyLower.Contains("return") || subjectLower.Contains("refund"))
            {
                intent = "Refund";
                category = "Refund Request";
            }
            else if (bodyLower.Contains("shipping") || bodyLower.Contains("track") || bodyLower.Contains("delivery") || bodyLower.Contains("order") || subjectLower.Contains("order"))
            {
                intent = "Shipping";
                category = "Order Tracking";
            }
            else if (bodyLower.Contains("login") || bodyLower.Contains("password") || bodyLower.Contains("lock"))
            {
                intent = "TechSupport";
                category = "Account Lockout";
            }
            
            // Extract potential 5-digit order numbers
            var match = Regex.Match(ticket.Body, @"\b\d{5}\b");
            if (match.Success)
            {
                orderId = match.Value;
            }

            // Extract customer name if email has standard format or name pattern
            if (ticket.CustomerEmail.Contains("jane.doe")) customerName = "Jane Doe";
            if (ticket.CustomerEmail.Contains("john.doe")) customerName = "John Doe";
            if (ticket.CustomerEmail.Contains("alice.smith")) customerName = "Alice Smith";
            if (ticket.CustomerEmail.Contains("admin")) customerName = "Zaid (Admin)";

            var metadataObj = new Dictionary<string, string?>
            {
                { "orderId", orderId == "null" ? null : orderId },
                { "accountId", ticket.CustomerEmail.Contains("alice") ? "ACC-5421" : null },
                { "customerName", customerName }
            };

            ticket.Intent = intent;
            ticket.Category = category;
            ticket.MetadataJson = JsonSerializer.Serialize(metadataObj);

            log.Output = $$"""
            {
              "intent": "{{intent}}",
              "category": "{{category}}",
              "metadata": {
                "orderId": {{(orderId == "null" ? "null" : $"\"{orderId}\"")}},
                "accountId": {{(ticket.CustomerEmail.Contains("alice") ? "\"ACC-5421\"" : "null")}},
                "customerName": "{{customerName}}"
              }
            }
            
            [OFFLINE SIMULATION LOG]: Automatically classified intent using local keyword matching rules.
            """;
            
            log.Status = "Success";
            return log;
        }

        var prompt = $$"""
            You are an Intent Classification Agent for a customer support team.
            Your job is to read the customer email and classify the intent and extract key metadata (such as Order ID, Customer Name, and Account ID) if present.

            Classify the intent into one of these categories:
            - Billing: Payments, invoice requests, subscription issues.
            - Shipping: Delivery status, tracking number requests, address updates.
            - Refund: Refund requests, returns, chargebacks.
            - TechSupport: Website issues, login problems, bug reports.
            - General: Standard questions, partnerships, feedback.

            You MUST respond ONLY with a JSON object in the following format:
            {
              "intent": "IntentCategory",
              "category": "DetailedCategory",
              "metadata": {
                "orderId": "extracted_order_id_or_null",
                "accountId": "extracted_account_id_or_null",
                "customerName": "extracted_customer_name_or_null"
              }
            }

            Email Subject: {{ticket.Subject}}
            Email Body:
            {{ticket.Body}}

            Response JSON:
            """;

        try
        {
            var response = await _kernel.InvokePromptAsync<string>(prompt);
            var responseText = response?.Trim() ?? string.Empty;

            if (responseText.StartsWith("```json"))
            {
                responseText = responseText.Substring(7).Trim();
            }
            if (responseText.EndsWith("```"))
            {
                responseText = responseText.Substring(0, responseText.Length - 3).Trim();
            }

            log.Output = responseText;

            using (var jsonDoc = JsonDocument.Parse(responseText))
            {
                var root = jsonDoc.RootElement;
                ticket.Intent = root.GetProperty("intent").GetString();
                ticket.Category = root.GetProperty("category").GetString();
                ticket.MetadataJson = root.GetProperty("metadata").ToString();
            }

            log.Status = "Success";
        }
        catch (Exception ex)
        {
            log.Status = "Error";
            log.Output = $"Failed to classify intent: {ex.Message}. Raw Output: {log.Output}";
            ticket.Intent = "General";
            ticket.Category = "Unclassified";
            ticket.MetadataJson = "{}";
        }

        return log;
    }
}
