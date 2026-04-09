using ErrorOr;

namespace FileKeeper.Core.Models.Errors;

public static class CommonErrors
{
    public static readonly Error OperationCanceled =
        Error.Unexpected(
            code: "Operation.Canceled",
            description: "The operation was canceled.");
}