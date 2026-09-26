// swift-tools-version:5.10
// ADB Manager for macOS. Build the Rust core first (see README):
//   cargo build -p adbm-ffi --release
//   swift build -c release -Xlinker -L<dir containing libadbm_ffi.a>
import PackageDescription

let package = Package(
    name: "AdbManager",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "AdbManager", targets: ["AdbManager"])],
    targets: [
        // C header of the Rust core (crates/adbm-ffi/include/adbm.h).
        .systemLibrary(name: "CAdbm", path: "Sources/CAdbm"),
        .executableTarget(
            name: "AdbManager",
            dependencies: ["CAdbm"],
            linkerSettings: [.linkedFramework("CoreFoundation"), .linkedFramework("Security"), .linkedLibrary("resolv"), .linkedLibrary("iconv")]
        ),
    ]
)
