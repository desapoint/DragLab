namespace DragLab.Runtime
{
    // Compatibility readout for the native verification harness; attribution is
    // recorded by Domain.RuntimeDataEncodingProbe's synchronous thread-local scopes.
    internal static class PartConsumerEncoderProbe
    {
        internal static long PartConsumerJsonCallCount=>DragLab.Domain.Mechanics.RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
        internal static long PartConsumerLegacyKeyCallCount=>DragLab.Domain.Mechanics.RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
        internal static string LastOperation=>DragLab.Domain.Mechanics.RuntimeDataEncodingProbe.LastOperation;
    }
}
