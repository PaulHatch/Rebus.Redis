# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Fixed
- Improve async void exception handling in Redis subscription handler to prevent process crashes
- Replace throwing exceptions with logging for unrecoverable errors (null serializer, null values)
- Add comprehensive try-catch wrapping to HandleResponseMessage
- Handle deserialization failures gracefully by setting TaskCompletionSource to faulted state
- Fix race condition with initialization lock

### Added
- Add inner exception support to RedisAsyncException for better error diagnostics
- Add UnreachableException polyfill for pre-.NET 7.0 targets

### Changed
- Redis subscription errors are now logged instead of crashing the application
- Unknown response types now set the pending task to faulted state instead of silently returning
- **CI/CD**: Updated GitHub Actions workflow to use build-once, promote-everywhere pattern

## [0.0.1] - Initial Release

### Added
- Redis saga storage implementation
- Redis subscription storage implementation  
- Redis outbox support using streams
- Async request/reply pattern using Redis pub/sub
- Support for scatter/gather messaging patterns
- Reply context for deferred responses in sagas