namespace MyUpwayMQTTBridge
{
    public readonly record struct Sensor(
        string Name,
        int ID,
        string Identifier,
        string? DeviceClass,
        string? StateClass,
        string? UnitOfMeasurement,
        string? ValueTemplate = null
    );
}
