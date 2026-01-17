/// Severity level for diagnostics
#[repr(u8)]
pub enum DiagnosticSeverity {
    Error = 0,
    Warning = 1,
    Hint = 2,
}

/// Source code location information
#[repr(C)]
pub struct SourceLocation {
    /// 1-indexed line number (0 if unavailable)
    pub line: u32,
    /// 1-indexed column number (0 if unavailable)
    pub column: u32,
    /// Length of the span in characters (0 if unavailable)
    pub length: u32,
}

/// A single diagnostic message
#[repr(C)]
pub struct Diagnostic {
    pub severity: DiagnosticSeverity,
    /// UTF-8 message bytes
    pub message: *mut u8,
    pub message_len: usize,
    /// Location (all zeros if unavailable)
    pub location: SourceLocation,
}

/// Buffer containing UTF-8 or binary data
#[repr(C)]
pub struct Buffer {
    pub data: *mut u8,
    pub len: usize,
}

/// Array of buffers (for multipage SVG)
#[repr(C)]
pub struct BufferArray {
    pub buffers: *mut Buffer,
    pub len: usize,
}

/// Result of a compilation operation
#[repr(C)]
pub struct CompileResult {
    /// True if compilation succeeded
    pub success: bool,
    /// Array of diagnostics (always present, even if empty)
    pub diagnostics: *mut Diagnostic,
    pub diagnostics_len: usize,
    /// Opaque document handle (null if compilation failed)
    pub document: *mut std::ffi::c_void,
}

#[repr(C)]
#[derive(Copy, Clone, Default)]
pub struct CompilerOptions {
    /// Include system fonts (default: true)
    pub include_system_fonts: bool,
    /// JSON string of inputs {"key" : "content"}
    pub inputs_json: *const u8,
    pub inputs_json_len: usize,
    /// Custom font paths (array of UTF-8 strings)
    pub custom_font_paths: *const u8,
    pub custom_font_paths_len: usize,
    /// Package path for offline packages
    pub package_path: *const u8,
    pub package_path_len: usize,
    // future additions: e.g. PDF output options down here
    // pub pdf_standard: u8,
    // pub pdf_tagged: bool, etc...
}

impl Default for CompileResult {
    fn default() -> Self {
        Self {
            success: false,
            diagnostics: std::ptr::null_mut(),
            diagnostics_len: 0,
            document: std::ptr::null_mut(),
        }
    }
}

impl Default for SourceLocation {
    fn default() -> Self {
        Self {
            line: 0,
            column: 0,
            length: 0,
        }
    }
}
