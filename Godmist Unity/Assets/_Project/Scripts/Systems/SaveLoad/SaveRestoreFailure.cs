using System;

namespace Godmist.Systems.SaveLoad
{
    public enum SaveRestoreErrorCode
    {
        InvalidValue,
        MissingValue,
        UnsupportedVersion,
        UnknownReference,
        DuplicateId,
        AlreadyAssigned,
        UnassignedItem,
        ContractRejected,
        ReconstructionFailed
    }

    /// <summary>Transient diagnostic only; not part of the save format.</summary>
    public sealed class SaveRestoreFailure
    {
        public SaveRestoreErrorCode Code { get; }
        public string Stage { get; }
        public string FieldPath { get; }
        public string EntityId { get; }
        public string Detail { get; }

        internal SaveRestoreFailure(SaveRestoreErrorCode code, string stage, string path, string id, string detail)
        { Code = code; Stage = stage; FieldPath = path; EntityId = id ?? string.Empty; Detail = detail; }

        public string UserMessage => $"Nie udało się odtworzyć zapisu. Etap: {Stage}; pole: {FieldPath}; kod: {Code}.";
        public override string ToString() => $"{UserMessage} ID: '{EntityId}'. {Detail}";
    }

    /// <summary>Explicit per-call trace. Child contexts share only the first failure.</summary>
    internal sealed class RestoreContext
    {
        private sealed class Result { public SaveRestoreFailure Failure; }
        private readonly Result result;
        private readonly string path, stage, entityId;
        public SaveRestoreFailure Failure => result.Failure;

        public RestoreContext() : this(new Result(), "$", "Header", string.Empty) { }
        private RestoreContext(Result result, string path, string stage, string entityId)
        { this.result = result; this.path = path; this.stage = stage; this.entityId = entityId; }

        public RestoreContext Field(string name, string nextStage = null)
            => new(result, string.IsNullOrEmpty(name) ? path : path + "." + name, nextStage ?? stage, entityId);

        public RestoreContext Entry(int index, string id = null)
            => new(result, path + "[" + index + "]", stage, id ?? entityId);

        public RestoreContext Entity(string id) => new(result, path, stage, id ?? entityId);

        public bool Reject(SaveRestoreErrorCode code, string field, string detail)
        {
            result.Failure ??= new SaveRestoreFailure(code, stage,
                string.IsNullOrEmpty(field) ? path : path + "." + field, entityId, detail);
            return false;
        }

        public bool Exception(Exception exception)
            => Reject(SaveRestoreErrorCode.ReconstructionFailed, string.Empty,
                exception.GetType().Name + ": " + exception.Message);

        public bool Require(bool success, string field, string detail)
            => success || Reject(SaveRestoreErrorCode.ContractRejected, field, detail);
    }
}
