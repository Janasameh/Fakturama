namespace F2C;

/// Ambiguous or conflicting data: a human must decide. Never retried.
public class ManualReviewException(string m) : Exception(m);
/// A step's postcondition did not hold.
public class StepFailedException(string m) : Exception(m);
/// Extracted data is internally inconsistent.
public class ValidationException(string m) : Exception(m);
