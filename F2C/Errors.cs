namespace F2C;

public class ManualReviewException(string m) : Exception(m);
public class StepFailedException(string m) : Exception(m);
public class ValidationException(string m) : Exception(m);
