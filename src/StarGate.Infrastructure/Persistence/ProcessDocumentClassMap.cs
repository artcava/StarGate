using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// BsonClassMap configuration for ProcessDocument.
/// Explicitly configures Guid serialization to use Standard representation.
/// Required for MongoDB.Driver 2.28.0+ to ensure consistent Guid handling.
/// </summary>
public static class ProcessDocumentClassMap
{
    private static bool _isRegistered;
    private static readonly object _lock = new();

    public static void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        lock (_lock)
        {
            if (_isRegistered)
            {
                return;
            }

            // Check if already registered by MongoDB conventions
            if (BsonClassMap.IsClassMapRegistered(typeof(ProcessDocument)))
            {
                _isRegistered = true;
                return;
            }

            // Register the class map with explicit Guid serializer
            BsonClassMap.RegisterClassMap<ProcessDocument>(cm =>
            {
                cm.AutoMap();
                
                // Explicitly map ProcessId as _id with Standard GuidSerializer
                cm.MapIdMember(c => c.ProcessId)
                    .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
            });

            _isRegistered = true;
        }
    }
}
