// FFI SURFACE: C-Compatible exports for .NET interop
// This is the only public API surface.

mod compiler;
mod document;
mod memory;
#[allow(non_camel_case_types)]
mod types;
mod typst_backend;

use std::path::PathBuf;
use std::ptr;
use std::slice;

use crate::types::CompilerOptions;
use compiler::{CompilerInstance, DocumentInstance};
use types::{Buffer, BufferArray, CompileResult};
// ============================================================================
// VERSION INFORMATION
// ============================================================================

pub const TYPST_NET_VERSION: &str = env!("CARGO_PKG_VERSION");
pub const TYPST_VERSION: &str = env!("TYPST_VERSION");

#[unsafe(no_mangle)]
pub extern "C" fn typst_net_version() -> *const u8 {
    TYPST_NET_VERSION.as_ptr()
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_net_version_len() -> usize {
    TYPST_NET_VERSION.len()
}

// ============================================================================
// COMPILER LIFECYCLE
// ============================================================================

/// Create a new compiler instance
///
/// # Arguments
/// * `root_path` - UTF-8 encoded path to workspace root
/// * `root_path_len` - Length of root_path in bytes
/// * `options` - Compiler configuration options (can be null for defaults)
///
/// # Options fields (all optional, pass null struct for defaults):
/// * `include_system_fonts` - Whether to load system fonts (default: true)
/// * `inputs_json` - JSON object string of inputs: {"key": "value"}
/// * `custom_font_paths` - Array of font directory paths (TODO: not yet implemented)
/// * `package_path` - Path for offline packages
///
/// # Returns
/// Opaque pointer to compiler instance, or null on failure
///
/// # Safety
/// * `root_path` must point to valid UTF-8 bytes
/// * `options` pointers must remain valid during this call
/// * Caller must free returned pointer with `typst_net_compiler_free`
/// * All memory in `options` is borrowed - caller retains ownership
///
/// # Memory
/// This function does NOT take ownership of any pointers in `options`.
/// Caller may free option strings immediately after this call returns.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_compiler_create(
    root_path: *const u8,
    root_path_len: usize,
    options: *const CompilerOptions,
) -> *mut std::ffi::c_void {
    if root_path.is_null() || root_path_len == 0 {
        return ptr::null_mut();
    }

    unsafe {
        let path_bytes = slice::from_raw_parts(root_path, root_path_len);
        let path_str = match std::str::from_utf8(path_bytes) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        };

        let root = PathBuf::from(path_str);

        let opts = if options.is_null() {
            CompilerOptions::default()
        } else {
            *options
        };

        match CompilerInstance::new(root, &opts) {
            Ok(compiler) => Box::into_raw(Box::new(compiler)) as *mut std::ffi::c_void,
            Err(_) => ptr::null_mut(),
        }
    }
}

/// Free a compiler instance
///
/// # Safety
/// - `compiler` must be a valid pointer from `typst_net_compiler_create`
/// - `compiler` must not be used after this call
/// - Must only be called once per compiler
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_compiler_free(compiler: *mut std::ffi::c_void) {
    if !compiler.is_null() {
        unsafe {
            let _ = Box::from_raw(compiler as *mut CompilerInstance);
        }
    }
}

// ============================================================================
// COMPILATION
// ============================================================================

/// Compile typst source code
///
/// # Arguments
/// * `compiler` - Valid compiler pointer
/// * `source` - UTF-8 encoded source code
/// * `source_len` - Length of source in bytes
///
/// # Returns
/// CompileResult - caller must free with `typst_net_compile_result_free`
///
/// # Safety
/// - `compiler` must be a valid pointer from `typst_net_compiler_create`
/// - `source` must be valid UTF-8
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_compiler_compile(
    compiler: *mut std::ffi::c_void,
    source: *const u8,
    source_len: usize,
) -> CompileResult {
    if compiler.is_null() {
        return CompileResult {
            success: false,
            diagnostics: ptr::null_mut(),
            diagnostics_len: 0,
            document: ptr::null_mut(),
        };
    }

    unsafe {
        let compiler = &mut *(compiler as *mut CompilerInstance);

        let source_str = if source.is_null() || source_len == 0 {
            "" // empty source is valid
        } else {
            let source_bytes = slice::from_raw_parts(source, source_len);
            match std::str::from_utf8(source_bytes) {
                Ok(s) => s,
                Err(_) => {
                    return CompileResult {
                        success: false,
                        diagnostics: ptr::null_mut(),
                        diagnostics_len: 0,
                        document: ptr::null_mut(),
                    };
                }
            }
        };

        compiler.update_source(source_str);
        compiler.compile()
    }
}

/// Free a compilation result
///
/// # Safety
/// - `result` must be from a `typst_net_compiler_compile` call
/// - must only be called once per result
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_result_free(result: CompileResult) {
    unsafe {
        // Free diagnostics
        if !result.diagnostics.is_null() && result.diagnostics_len > 0 {
            memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }

        // Free document if present
        if !result.document.is_null() {
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
        }
    }
}

// ============================================================================
// DOCUMENT OPERATIONS
// ============================================================================

/// Get the number of pages in a document
///
/// # Safety
/// - `document` must be a valid pointer from a successful CompileResult
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_document_page_count(document: *const std::ffi::c_void) -> usize {
    unsafe { document::document_page_count(document as *const DocumentInstance) }
}

/// Render a single page to SVG
///
/// # Arguments
/// * `document` - Valid document pointer
/// * `page_index` - 0-indexed page number
///
/// # Returns
/// Buffer containing SVG data - caller must free with `typst_net_buffer_free`
///
/// # Safety
/// - `document` must be a valid pointer from a successful CompileResult
/// - `page_index` must be < page_count
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_document_render_svg_page(
    document: *const std::ffi::c_void,
    page_index: usize,
) -> Buffer {
    unsafe { document::document_render_page_svg(document as *const DocumentInstance, page_index) }
}

/// Render all pages to SVG
///
/// # Returns
/// BufferArray containing SVG data for each page - caller must free with `typst_net_buffer_array_free`
///
/// # Safety
/// - `document` must be a valid pointer from a successful CompileResult
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_document_render_svg_all(
    document: *const std::ffi::c_void,
) -> BufferArray {
    unsafe { document::document_render_all_pages_svg(document as *const DocumentInstance) }
}

/// Render document to PDF
///
/// # Note
/// Currently unimplemented - returns empty buffer
///
/// # Safety
/// - `document` must be a valid pointer from a successful CompileResult
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_document_render_pdf(
    document: *const std::ffi::c_void,
) -> Buffer {
    unsafe { document::document_render_pdf(document as *const DocumentInstance) }
}

// ============================================================================
// MEMORY MANAGEMENT
// ============================================================================

/// Free a buffer
///
/// # Safety
/// - `buffer` must be from a typst_net_* function
/// - Must only be called once per buffer
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_buffer_free(buffer: Buffer) {
    unsafe {
        memory::free_buffer(buffer);
    }
}

/// Free a buffer array
///
/// # Safety
/// - `array` must be from typst_net_document_render_svg_all
/// - Must only be called once per array
#[unsafe(no_mangle)]
pub unsafe extern "C" fn typst_net_buffer_array_free(array: BufferArray) {
    unsafe {
        memory::free_buffer_array(array);
    }
}

// ============================================================================
// CACHE MANAGEMENT
// ============================================================================

/// Reset the compilation cache
///
/// For long-running processes, call this periodically to evict old cached data
///
/// # Arguments
/// * `max_age_seconds` - Evict cache entries older than this (0 = evict all)
#[unsafe(no_mangle)]
pub extern "C" fn typst_net_reset_cache(max_age_seconds: usize) {
    comemo::evict(max_age_seconds);
}

// ============================================================================
// TESTS
// ============================================================================
#[cfg(test)]
mod ffi_tests {
    use super::*;
    use crate::types::CompilerOptions;
    use std::ptr;

    //noinspection DuplicatedCode
    fn default_options() -> CompilerOptions {
        CompilerOptions {
            include_system_fonts: true,
            inputs_json: ptr::null(),
            inputs_json_len: 0,
            custom_font_paths: ptr::null(),
            custom_font_paths_len: 0,
            package_path: ptr::null(),
            package_path_len: 0,
        }
    }

    #[test]
    fn test_version_exports() {
        let version_ptr = typst_net_version();
        let version_len = typst_net_version_len();

        assert!(!version_ptr.is_null());
        assert!(version_len > 0);

        unsafe {
            let version_bytes = slice::from_raw_parts(version_ptr, version_len);
            let version_str = std::str::from_utf8(version_bytes).unwrap();
            assert!(!version_str.is_empty());
        }
    }

    #[test]
    fn test_full_ffi_lifecycle() {
        unsafe {
            // Create compiler
            let root = std::env::temp_dir();
            let root_str = root.to_str().unwrap();
            let options = default_options();

            let compiler = typst_net_compiler_create(root_str.as_ptr(), root_str.len(), &options);
            assert!(!compiler.is_null());

            // Compile
            let source = "= Hello FFI\n\nTest content.";
            let result = typst_net_compiler_compile(compiler, source.as_ptr(), source.len());

            assert!(result.success);
            assert!(!result.document.is_null());

            // Get page count
            let page_count = typst_net_document_page_count(result.document);
            assert_eq!(page_count, 1);

            // Render SVG
            let svg_buffer = typst_net_document_render_svg_page(result.document, 0);
            assert!(!svg_buffer.data.is_null());
            assert!(svg_buffer.len > 0);

            // Verify SVG
            let svg_bytes = slice::from_raw_parts(svg_buffer.data, svg_buffer.len);
            let svg_str = std::str::from_utf8(svg_bytes).unwrap();
            assert!(svg_str.contains("<svg") || svg_str.starts_with("<?xml"));

            // Cleanup
            typst_net_buffer_free(svg_buffer);
            typst_net_result_free(result);
            typst_net_compiler_free(compiler);
        }
    }

    #[test]
    fn test_null_safety() {
        unsafe {
            // Null compiler creation
            let compiler = typst_net_compiler_create(ptr::null(), 0, ptr::null());
            assert!(compiler.is_null());

            // Null compilation
            let result = typst_net_compiler_compile(ptr::null_mut(), ptr::null(), 0);
            assert!(!result.success);
            assert!(result.document.is_null());

            // Null document operations
            let count = typst_net_document_page_count(ptr::null());
            assert_eq!(count, 0);

            let buffer = typst_net_document_render_svg_page(ptr::null(), 0);
            assert!(buffer.data.is_null());
        }
    }

    #[test]
    fn test_render_all_pages_ffi() {
        unsafe {
            let root = std::env::temp_dir();
            let root_str = root.to_str().unwrap();
            let options = default_options();

            let compiler = typst_net_compiler_create(root_str.as_ptr(), root_str.len(), &options);

            let source = "= Page 1\n#pagebreak()\n= Page 2";
            let result = typst_net_compiler_compile(compiler, source.as_ptr(), source.len());

            assert!(result.success);

            let array = typst_net_document_render_svg_all(result.document);
            assert!(!array.buffers.is_null());
            assert_eq!(array.len, 2);

            // Cleanup
            typst_net_buffer_array_free(array);
            typst_net_result_free(result);
            typst_net_compiler_free(compiler);
        }
    }

    #[test]
    fn test_cache_reset() {
        // Should not panic
        typst_net_reset_cache(30);
        typst_net_reset_cache(0);
    }
}
