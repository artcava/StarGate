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

            // Register global GuidSerializer with Standard representation (subType 03)
            // This must match MongoClientSettings.GuidRepresentation
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // Already registered - safe to ignore
            }

            // Register class map for ProcessDocument
            if (!BsonClassMap.IsClassMapRegistered(typeof(ProcessDocument)))
            {
                BsonClassMap.RegisterClassMap<ProcessDocument>(cm =>
                {
                    cm.AutoMap();
                    
                    // Map ProcessId as _id with Standard GuidSerializer (subType 03)
                    cm.MapIdMember(c => c.ProcessId)
                        .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                });
            }

            _isRegistered = true;
        }
    }
}
