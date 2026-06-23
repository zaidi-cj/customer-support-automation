using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CustomerSupportAgent.Infrastructure.Plugins;

public class OrderPlugin
{
    private static readonly Dictionary<string, OrderDetails> OrderDb = new(StringComparer.OrdinalIgnoreCase)
    {
        { "99482", new OrderDetails("99482", "Delivered", "2026-06-20", "UPS", "https://ups-tracking-sim.com/track/99482") },
        { "10293", new OrderDetails("10293", "Shipped", "2026-06-26", "FedEx - Tracking ID: 1Z999AA1012", "https://fedex-sim.com/track/10293") },
        { "55432", new OrderDetails("55432", "Processing", "2026-06-28 (Est.)", "Pending", "N/A") }
    };

    [KernelFunction]
    [Description("Looks up details for a customer's order by its order ID, returning status, shipping carrier, and estimated delivery date.")]
    public string GetOrderDetails(
        [Description("The 5-digit Order ID (e.g. 99482, 10293, 55432)")] string orderId)
    {
        if (OrderDb.TryGetValue(orderId, out var order))
        {
            return $"Order ID: {order.OrderId}\nStatus: {order.Status}\nEstimated Delivery: {order.EstDelivery}\nCarrier: {order.Carrier}\nTracking URL: {order.TrackingUrl}";
        }
        return $"Order ID '{orderId}' was not found in the order database. Check the ID spelling or ask the customer for details.";
    }

    private record OrderDetails(string OrderId, string Status, string EstDelivery, string Carrier, string TrackingUrl);
}
