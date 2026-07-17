namespace UserService;

/// <summary>
/// Точка привязки для WebApplicationFactory в тестах: по сборке этого типа
/// фабрика находит точку входа приложения. Сгенерированный Program лежит в
/// глобальном пространстве имён и в E2E конфликтует с Program другого сервиса.
/// </summary>
public sealed class ApiMarker;
