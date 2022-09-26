using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MQTTnet.Extensions.ManagedClient;
using Polly;
using Polly.Retry;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MyUpwayMQTTBridge
{
    public class MQTTService : BackgroundService
    {
        private readonly IManagedMqttClient _mqttClient;
        private readonly ManagedMqttClientOptions _mqttClientOptions;
        private readonly string _mqttTopic;
        private readonly string _mqttDiscoveryPrefix;
        private readonly HttpClient _myUpwayHttpClient;
        private readonly int _updateInterval;
        private readonly string _myUpwayUsername;
        private readonly string _myUpwayPassword;
        private readonly string _systemId;

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public MQTTService(IManagedMqttClient client, ManagedMqttClientOptions clientOptions, IConfiguration configuration)
        {
            // Setup MQTT client
            _mqttClient = client;
            _mqttClientOptions = clientOptions;
            _mqttTopic = configuration.GetValue<string>("MQTT_TOPIC").Trim('/');
            _mqttDiscoveryPrefix = configuration.GetValue<string>("MQTT_DISCOVERY_PREFIX").Trim('/');

            // Setup myUpway HTTP client
            var cookieContainer = new CookieContainer();
            var httpClientHandler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookieContainer,
                AllowAutoRedirect = false
            };
            _myUpwayHttpClient = new HttpClient(httpClientHandler)
            {
                BaseAddress = new Uri("https://myupway.com/")
            };
            _updateInterval = configuration.GetValue<int>("UPDATE_INTERVAL");
            _myUpwayUsername = configuration.GetValue<string>("MYUPWAY_USERNAME");
            _myUpwayPassword = configuration.GetValue<string>("MYUPWAY_PASSWORD");
            _systemId = configuration.GetValue<string>("MYUPWAY_SYSTEM_ID");
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // Start client
            await _mqttClient.StartAsync(_mqttClientOptions);

            await AuthenticateMyUpwayClient();

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            // Wait until the queue is fully processed.
            SpinWait.SpinUntil(() => _mqttClient.PendingApplicationMessagesCount == 0, 10000);

            Console.WriteLine($"Pending messages = {_mqttClient.PendingApplicationMessagesCount}");

            await _mqttClient.StopAsync();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Do this once
            await PublishAutoDiscovery();
            await UpdateValues();

            // Update values at interval
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(_updateInterval));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await UpdateValues();
            }
        }

        public async Task PublishAutoDiscovery()
        {
            // Add data sensors
            foreach (var sensor in Variables.Sensors)
            {
                var payload = new
                {
                    name = sensor.Name,
                    unique_id = $"{_mqttTopic}_{sensor.Identifier}",
                    device_class = sensor.DeviceClass,
                    state_class = sensor.StateClass,
                    unit_of_measurement = sensor.UnitOfMeasurement,
                    state_topic = $"{_mqttTopic}/sensor/{sensor.Identifier}",
                    value_template = sensor.ValueTemplate,
                    device = new
                    {
                        name = _mqttTopic,
                        identifiers = _mqttTopic,
                        manufacturer = "Metro Therm",
                        model = "SHK200S"
                    }
                };
                var payloadJson = JsonSerializer.Serialize(payload, _serializerOptions);
                await _mqttClient.EnqueueAsync($"{_mqttDiscoveryPrefix}/sensor/{_mqttTopic}/{sensor.Identifier}/config", payloadJson, retain: true);
            }
            foreach (var sensor in Variables.BinarySensors)
            {
                var payload = new
                {
                    name = sensor.Name,
                    unique_id = $"{_mqttTopic}_{sensor.Identifier}",
                    device_class = sensor.DeviceClass,
                    state_topic = $"{_mqttTopic}/sensor/{sensor.Identifier}",
                    payload_on = sensor.PayloadOn,
                    payload_off = sensor.PayloadOff,
                    device = new
                    {
                        name = _mqttTopic,
                        identifiers = _mqttTopic,
                        manufacturer = "Metro Therm",
                        model = "SHK200S"
                    }
                };
                var payloadJson = JsonSerializer.Serialize(payload, _serializerOptions);
                await _mqttClient.EnqueueAsync($"{_mqttDiscoveryPrefix}/binary_sensor/{_mqttTopic}/{sensor.Identifier}/config", payloadJson, retain: true);
            }
        }

        public async Task UpdateValues()
        {
            // Request sensor values
            var variableIds = Variables.Sensors.Select(s => s.ID).Concat(Variables.BinarySensors.Select(s => s.ID));
            var valuesResponse = await MyUpwayReauthenticatePolicy()
                .ExecuteAsync(async () => await _myUpwayHttpClient.PostAsync("PrivateAPI/Values", new FormUrlEncodedContent(
                    new[]
                    {
                        new KeyValuePair<string, string>("hpid", _systemId)
                    }.Concat(
                        variableIds.Select(s => new KeyValuePair<string, string>("variables", s.ToString()))
                    )
                )));

            // Check if not redirected to login page
            if (IsUnauthorizedResponse(valuesResponse))
            {
                throw new InvalidOperationException("Not authenticated");
            }

            var jsonResponse = await valuesResponse.Content.ReadAsStringAsync();
            var payloadJson = JsonNode.Parse(jsonResponse);
            var prefix = $"{_mqttTopic}/sensor";

            // Push data sensors
            var sensorValues = payloadJson["Values"].AsArray().ToDictionary(n => n["VariableId"].GetValue<int>(), n => n["CurrentValue"].GetValue<string>());
            foreach (var sensor in Variables.Sensors)
            {
                var value = sensorValues.GetValueOrDefault(sensor.ID);
                if (value != null && value != "--")
                {
                    await _mqttClient.EnqueueAsync($"{prefix}/{sensor.Identifier}", value);
                }
            }
            foreach (var sensor in Variables.BinarySensors)
            {
                var value = sensorValues.GetValueOrDefault(sensor.ID);
                if (value != null)
                {
                    await _mqttClient.EnqueueAsync($"{prefix}/{sensor.Identifier}", value);
                }
            }
        }

        private AsyncRetryPolicy<HttpResponseMessage> MyUpwayReauthenticatePolicy()
            => Policy
                .HandleResult<HttpResponseMessage>(r => IsUnauthorizedResponse(r))
                .RetryAsync((_, _) => AuthenticateMyUpwayClient());

        private bool IsUnauthorizedResponse(HttpResponseMessage httpResponse)
            => httpResponse.Headers.Location?.OriginalString.Split("?")[0] == "/LogIn";

        private async Task AuthenticateMyUpwayClient()
        {
            var returnUrl = $"/system/{_systemId}/Status/Overview";
            var loginResponse = await _myUpwayHttpClient.PostAsync("LogIn", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Email", _myUpwayUsername),
                new KeyValuePair<string, string>("Password", _myUpwayPassword),
                new KeyValuePair<string, string>("ReturnUrl", returnUrl)
            }));

            // Check if login succeeded by checking if redirected to the return url
            if (loginResponse.Headers.Location?.OriginalString != returnUrl)
            {
                throw new InvalidOperationException("Login failed");
            }
        }
    }
}
