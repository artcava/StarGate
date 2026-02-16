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

            // CRITICAL: Use GuidRepresentation.Unspecified
            // This matches the behavior of BsonBinaryData created with BsonBinarySubType.UuidStandard
            // The subType (04) defines the actual binary format, not the GuidRepresentation enum
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Unspecified));
            }
            catch (BsonSerializationException)
            {
                // Already registered - safe to ignore
            }

            // Then register class map for document field serialization
            // Check if already registered by MongoDB conventions
            if (!BsonClassMap.IsClassMapRegistered(typeof(ProcessDocument)))
            {
                BsonClassMap.RegisterClassMap<ProcessDocument>(cm =>
                {
                    cm.AutoMap();
                    
                    // Map ProcessId as _id with Unspecified GuidRepresentation
                    // This allows it to work with any BsonBinarySubType
                    cm.MapIdMember(c => c.ProcessId)
                        .SetSerializer(new GuidSerializer(GuidRepresentation.Unspecified));
                });
            }

            _isRegistered = true;
        }
    }
}
