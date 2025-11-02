# Contributing to OxidizePdf.NET

Thank you for your interest in contributing to OxidizePdf.NET!

## Development Setup

### Prerequisites

- **Rust**: 1.77 or later ([install](https://rustup.rs/))
- **.NET SDK**: 6.0 or later ([install](https://dotnet.microsoft.com/download))
- **Git**: For version control

### Clone Repository

```bash
git clone https://github.com/bzsanti/oxidize-pdf-dotnet.git
cd oxidize-pdf-dotnet
```

### Build Native Library

```bash
cd native
cargo build --release
```

### Build .NET Wrapper

```bash
cd dotnet
dotnet build
```

## Project Structure

```
oxidize-pdf-dotnet/
├── native/           # Rust FFI layer
│   └── src/
│       └── lib.rs    # FFI functions
├── dotnet/           # C# wrapper
│   └── OxidizePdf.NET/
│       ├── PdfExtractor.cs
│       └── NativeMethods.cs
├── examples/         # Usage examples
│   ├── BasicUsage/
│   └── KernelMemory/
└── build/            # Build scripts
    └── build-native.sh
```

## Development Workflow

### 1. Create Feature Branch

```bash
git checkout -b feature/your-feature-name
```

### 2. Make Changes

- **Rust**: Edit files in `native/src/`
- **C#**: Edit files in `dotnet/OxidizePdf.NET/`

### 3. Run Tests

```bash
# Rust tests
cd native
cargo test

# .NET tests
cd dotnet
dotnet test
```

### 4. Format Code

```bash
# Rust
cargo fmt

# .NET
dotnet format
```

### 5. Commit Changes

```bash
git add .
git commit -m "feat: your feature description"
```

Use conventional commits:
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `test:` - Test additions/changes
- `refactor:` - Code refactoring

### 6. Push and Create PR

```bash
git push origin feature/your-feature-name
```

Then create a Pull Request on GitHub.

## Code Standards

### Rust

- Follow [Rust API Guidelines](https://rust-lang.github.io/api-guidelines/)
- All public functions must have documentation comments
- Run `cargo clippy` and fix all warnings
- Maintain 80%+ test coverage

### C#

- Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use XML documentation comments for public APIs
- Enable nullable reference types
- Async methods should end with `Async` suffix

## Testing

### Unit Tests

```bash
# Rust
cd native
cargo test

# C#
cd dotnet
dotnet test
```

### Integration Tests

Create test PDFs in `test-pdfs/` directory and add integration tests.

### Performance Tests

```bash
cd native
cargo bench
```

## Documentation

- Update `README.md` for user-facing changes
- Update `ARCHITECTURE.md` for design decisions
- Add XML comments to public C# APIs
- Add rustdoc comments to public Rust functions

## Pull Request Process

1. **Description**: Clearly describe what your PR does
2. **Tests**: Add tests for new features
3. **Documentation**: Update relevant docs
4. **CI**: Ensure all CI checks pass
5. **Review**: Wait for maintainer review

## Reporting Issues

### Bug Reports

Include:
- OS and architecture (Windows x64, Linux x64, etc.)
- .NET version (`dotnet --version`)
- Rust version (`rustc --version`)
- Minimal reproduction code
- Error messages and stack traces

### Feature Requests

Include:
- Use case description
- Expected behavior
- Example code (if applicable)

## License

By contributing, you agree that your contributions will be licensed under the AGPL-3.0 License.

## Questions?

Open an issue or start a discussion on GitHub.
