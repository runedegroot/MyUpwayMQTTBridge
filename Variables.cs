namespace MyUpwayMQTTBridge
{
    public static class Variables
    {
        public static readonly Sensor[] Sensors = new Sensor[]
        {
            new("Setpoint temperature",                47398, "setpoint_temperature",                "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Indoor temperature",                  40033, "indoor_temperature",                  "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Outdoor temperature",                 40004, "outdoor_temperature",                 "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Slave outdoor temperature",           44362, "slave_outdoor_temperature",           "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Average outdoor temperature",         40067, "avg_outdoor_temperature",             "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Calculated flow temperature",         43009, "calculated_flow_temperature",         "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Calculated flow temperature cooling", 44270, "calculated_flow_temperature_cooling", "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Flow temperature",                    40071, "flow_temperature",                    "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Return temperature",                  40152, "return_temperature",                  "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Evaporator temperature",              44363, "evaporator_temperature",              "temperature",  "measurement", "°C", "{{ value[:-2] }}"),
            new("Degree minutes",                      43005, "degree_minutes",                      null,           "measurement", "DM", "{{ value[:-2] }}"),
            new("Charge pump speed",                   44396, "charge_pump_speed",                   "power_factor", "measurement", "%",  "{{ value[:-1] }}"),
            new("Compressor operating time",           44071, "compressor_operating_time",           "duration",     "total",       "h",  "{{ value[:-1] }}"),
            new("Current compressor frequency",        44701, "current_compressor_freq",             "frequency",    "measurement", "Hz", "{{ value[:-2] }}"),
            new("Requested compressor frequency",      40782, "requested_compressor_freq",           "frequency",    "measurement", "Hz", "{{ value[:-2] }}"),
            new("Compressor starts",                   44069, "compressor_starts",                   null,           "total",       null, null)
        };

        public static readonly BinarySensor[] BinarySensors = new BinarySensor[]
        {
            new("Defrosting", 44703, "defrosting", "running", "yes", "no")
        };
    }
}
