namespace MyUpwayMQTTBridge
{
    public readonly record struct BinarySensor(
        string Name,
        int ID,
        string Identifier,
        string DeviceClass,
        string PayloadOn,
        string PayloadOff
    );
}
