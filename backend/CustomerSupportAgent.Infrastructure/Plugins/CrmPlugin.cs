using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CustomerSupportAgent.Infrastructure.Plugins;

public class CrmPlugin
{
    private static readonly Dictionary<string, CrmCustomer> CustomerDb = new(StringComparer.OrdinalIgnoreCase)
    {
        { "admin@gemini.com", new CrmCustomer("Zaid", "VIP", "2023-04-12", "$1,450.00") },
        { "alice.smith@test.com", new CrmCustomer("Alice Smith", "Standard", "2025-01-10", "$120.00") },
        { "jane.doe@gmail.com", new CrmCustomer("Jane Doe", "Standard", "2024-08-15", "$340.00") },
        { "john.doe@example.com", new CrmCustomer("John Doe", "VIP", "2022-11-30", "$2,100.00") }
    };

    [KernelFunction]
    [Description("Retrieves customer CRM profile details such as name, tier, membership date, and lifetime spend by email.")]
    public string GetCustomerProfile(
        [Description("The customer's email address")] string email)
    {
        if (CustomerDb.TryGetValue(email, out var customer))
        {
            return $"Customer Name: {customer.Name}\nAccount Tier: {customer.Tier}\nMember Since: {customer.JoinedDate}\nLifetime Value: {customer.LifetimeValue}";
        }
        return $"No customer profile found for email '{email}'. The user may be a guest or new customer.";
    }

    private record CrmCustomer(string Name, string Tier, string JoinedDate, string LifetimeValue);
}
