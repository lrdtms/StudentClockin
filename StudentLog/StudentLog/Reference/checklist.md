✅ .NET MAUI Best Practices Checklist (2026)
🧱 Architecture

 Use MVVM pattern (no business logic in Views)
 Use CommunityToolkit.Mvvm for ViewModels
 Separate layers:
 Core (models, interfaces)
 Application (services, use cases)
 Infrastructure (API, storage)
 UI (Views + ViewModels)


 Keep ViewModels testable (no platform dependencies)


🔌 Dependency Injection

 Register services in MauiProgram.cs
 Use:
 Singleton → shared services
 Transient → ViewModels


 Avoid manual instantiation (new) for services


🎨 UI / XAML

 Prefer XAML over C# UI
 Use compiled bindings (x:DataType)
 Avoid code-behind logic
 Use CollectionView instead of ListView
 Avoid wrapping CollectionView in ScrollView
 Use Grid instead of deeply nested layouts


🎯 Styling

 Use ResourceDictionaries for:
 Colors
 Fonts
 Styles


 Support light/dark themes


⚡ Performance

 Enable trimming (PublishTrimmed)
 Enable AOT in Release builds where possible
 Minimize visual tree depth
 Avoid unnecessary bindings
 Use virtualization-friendly controls


🌐 Networking

 Use HttpClientFactory via DI
 Do NOT instantiate HttpClient manually
 Use Refit or typed clients
 Handle:
 Timeouts
 Retries (Polly)
 Errors gracefully




💾 Storage

 Use Preferences for simple data
 Use SecureStorage for sensitive data
 Use SQLite for structured local storage


🔐 Security

 Never hardcode secrets
 Store tokens securely
 Validate API inputs
 Use HTTPS only


🔄 Navigation

 Use Shell navigation
 Use route-based navigation (GoToAsync)
 Support deep linking where needed


🧠 State Management

 Keep state inside ViewModels
 Use ObservableProperty
 Use RelayCommand
 Use WeakReferenceMessenger for messaging