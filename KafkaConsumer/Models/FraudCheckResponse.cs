using System.Dynamic;
namespace KafkaConsumer.Models;

public class FraudCheckResponse()
{
    public required bool IsFraud { get; set; }
    public required string Reason { get; set; }
}
