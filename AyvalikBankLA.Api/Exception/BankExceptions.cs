namespace AyvalikBankLA.Api.Exception;

public class CustomerNotFoundException(string message) : System.Exception(message);
public class AccountNotFoundException(string message) : System.Exception(message);
public class AccountNotOperableException(string message) : System.Exception(message);
public class InsufficientFundsException(string message) : System.Exception(message);
public class InvalidPasswordException(string message) : System.Exception(message);
public class PasswordReusedException(string message) : System.Exception(message);
public class UnauthorizedAccessException(string message) : System.Exception(message);
public class LimitExceededException(string message) : System.Exception(message);
