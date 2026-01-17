use crate::types::{Buffer, BufferArray, Diagnostic, SourceLocation};
use std::ptr;

/// Converts a String to a raw UTF-8 buffer owned by caller
///
/// Caller must free with `free_buffer`
pub fn string_to_buffer(s: String) -> Buffer {
    let mut bytes = s.into_bytes();
    bytes.shrink_to_fit();

    let buffer = Buffer {
        data: bytes.as_mut_ptr(),
        len: bytes.len(),
    };

    std::mem::forget(bytes);
    buffer
}

/// Converts a Vec<u8> to a raw buffer owned by caller
///
/// Caller must free with `free_buffer`
pub fn vec_to_buffer(mut v: Vec<u8>) -> Buffer {
    v.shrink_to_fit();

    let buffer = Buffer {
        data: v.as_mut_ptr(),
        len: v.len(),
    };

    std::mem::forget(v);
    buffer
}

/// Converts multiple Vec<u8> to a BufferArray owned by caller
///
/// Caller must free with `free_buffer_array`
pub fn vecs_to_buffer_array(vecs: Vec<Vec<u8>>) -> BufferArray {
    let mut buffers: Vec<Buffer> = vecs.into_iter().map(vec_to_buffer).collect();

    buffers.shrink_to_fit();

    let array = BufferArray {
        buffers: buffers.as_mut_ptr(),
        len: buffers.len(),
    };

    std::mem::forget(buffers);
    array
}

/// Creates a Diagnostic from components
pub fn create_diagnostic(
    severity: crate::types::DiagnosticSeverity,
    message: String,
    location: Option<(u32, u32, u32)>, // (line, column, length)
) -> Diagnostic {
    let message_buf = string_to_buffer(message);

    let location = location
        .map(|(line, column, length)| SourceLocation {
            line,
            column,
            length,
        })
        .unwrap_or_default();

    Diagnostic {
        severity,
        message: message_buf.data,
        message_len: message_buf.len,
        location,
    }
}

/// Converts Vec<Diagnostic> to raw array owned by caller
///
/// Caller must free with `free_diagnostics`
pub fn diagnostics_to_array(diagnostics: Vec<Diagnostic>) -> (*mut Diagnostic, usize) {
    if diagnostics.is_empty() {
        return (ptr::null_mut(), 0);
    }

    let mut diags = diagnostics;
    diags.shrink_to_fit();

    let ptr = diags.as_mut_ptr();
    let len = diags.len();

    std::mem::forget(diags);
    (ptr, len)
}

/// Frees a Buffer allocated by `string_to_buffer` or `vec_to_buffer`
///
/// # Safety
/// - Buffer must have been created by this module
/// - Buffer must not be used after this call
/// - Must only be called once per buffer
pub unsafe fn free_buffer(buffer: Buffer) {
    unsafe {
        if !buffer.data.is_null() && buffer.len > 0 {
            let _ = Vec::from_raw_parts(buffer.data, buffer.len, buffer.len);
        }
    }
}

/// Frees a BufferArray allocated by `vecs_to_buffer_array`
///
/// # Safety
/// - Array must have been created by `vecs_to_buffer_array`
/// - Array must not be used after this call
/// - Must only be called once per array
pub unsafe fn free_buffer_array(array: BufferArray) {
    unsafe {
        if !array.buffers.is_null() && array.len > 0 {
            let buffers = Vec::from_raw_parts(array.buffers, array.len, array.len);
            for buffer in buffers {
                free_buffer(buffer);
            }
        }
    }
}

/// Frees a diagnostic array
///
/// # Safety
/// - Diagnostics must have been created by `diagnostics_to_array`
/// - Diagnostics must not be used after this call
/// - Must only be called once
pub unsafe fn free_diagnostics(diagnostics: *mut Diagnostic, len: usize) {
    unsafe {
        if !diagnostics.is_null() && len > 0 {
            let diags = Vec::from_raw_parts(diagnostics, len, len);
            for diag in diags {
                if !diag.message.is_null() && diag.message_len > 0 {
                    let _ = Vec::from_raw_parts(diag.message, diag.message_len, diag.message_len);
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_string_to_buffer() {
        let s = "hello world".to_string();
        let buffer = string_to_buffer(s);

        assert!(!buffer.data.is_null());
        assert_eq!(buffer.len, 11);

        unsafe {
            free_buffer(buffer);
        }
    }

    #[test]
    fn test_vec_to_buffer() {
        let v = vec![1, 2, 3, 4, 5];
        let buffer = vec_to_buffer(v);

        assert!(!buffer.data.is_null());
        assert_eq!(buffer.len, 5);

        unsafe {
            free_buffer(buffer);
        }
    }

    #[test]
    fn test_vecs_to_buffer_array() {
        let vecs = vec![vec![1, 2, 3], vec![4, 5, 6]];
        let array = vecs_to_buffer_array(vecs);

        assert!(!array.buffers.is_null());
        assert_eq!(array.len, 2);

        unsafe {
            free_buffer_array(array);
        }
    }

    #[test]
    fn test_diagnostics_array_roundtrip() {
        let diag1 = create_diagnostic(
            crate::types::DiagnosticSeverity::Error,
            "error 1".to_string(),
            Some((10, 5, 3)),
        );
        
        let diag2 = create_diagnostic(
            crate::types::DiagnosticSeverity::Warning,
            "warning 1".to_string(),
            Some((20, 10, 5)),
        );
        
        let diagnostics = vec![diag1, diag2];
        let (ptr, len) = diagnostics_to_array(diagnostics);
        
        assert!(!ptr.is_null());
        assert_eq!(len, 2);
        
        unsafe {
            free_diagnostics(ptr, len);
        }
    }
    
    #[test]
    fn test_create_diagnostic_values() {
        let diag = create_diagnostic(
            crate::types::DiagnosticSeverity::Error,
            "test error".to_string(),
            Some((10, 5, 3)),
        );
        
        assert_eq!(diag.location.line, 10);
        assert_eq!(diag.location.column, 5);
        assert_eq!(diag.location.length, 3);
        assert!(!diag.message.is_null());
        assert_eq!(diag.message_len, "test error".len());
        
        unsafe {
            let _ = Vec::from_raw_parts(diag.message, diag.message_len, diag.message_len);
        }
    }
}
