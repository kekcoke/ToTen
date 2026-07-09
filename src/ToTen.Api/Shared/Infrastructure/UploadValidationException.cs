namespace ToTen.Api.Shared.Infrastructure;

// No upload endpoint exists yet (see audit finding 3.4) - a future endpoint should
// catch this explicitly and return 400 Bad Request rather than letting it fall
// through to GlobalExceptionHandler's generic 500.
public class UploadValidationException(string message) : Exception(message);
