use crate::compiler::DocumentInstance;
use crate::memory::{vec_to_buffer, vecs_to_buffer_array};
use crate::types::{Buffer, BufferArray};
use std::ptr;

/// Get the number of pages in a document
///
/// # Safety
/// - Document must be a valid pointer from a successful compilation
pub unsafe fn document_page_count(document: *const DocumentInstance) -> usize {
    if document.is_null() {
        return 0;
    }

    let doc = unsafe { &*document };
    doc.page_count()
}

/// Render a single page to SVG
///
/// # Safety
/// - Document must be a valid pointer from a successful compilation
/// - page_index must be < page_count
/// - Caller must free the returned buffer with `free_buffer`
pub unsafe fn document_render_page_svg(
    document: *const DocumentInstance,
    page_index: usize,
) -> Buffer {
    if document.is_null() {
        return Buffer {
            data: ptr::null_mut(),
            len: 0,
        };
    }

    let doc = unsafe { &*document };

    match doc.render_page_svg(page_index) {
        Ok(svg_bytes) => vec_to_buffer(svg_bytes),
        Err(_) => Buffer {
            data: ptr::null_mut(),
            len: 0,
        },
    }
}

/// Render all pages to SVG
///
/// # Safety
/// - Document must be a valid pointer from a successful compilation
/// - Caller must free the returned BufferArray with `free_buffer_array`
pub unsafe fn document_render_all_pages_svg(document: *const DocumentInstance) -> BufferArray {
    if document.is_null() {
        return BufferArray {
            buffers: ptr::null_mut(),
            len: 0,
        };
    };

    let doc = unsafe { &*document };

    match doc.render_all_pages_svg() {
        Ok(svg_pages) => vecs_to_buffer_array(svg_pages),
        Err(_) => BufferArray {
            buffers: ptr::null_mut(),
            len: 0,
        },
    }
}

/// Render entire document to PDF
///
/// # Safety
/// - Document must be a valid pointer from a successful compilation
/// - Caller must free the returned buffer with `free_buffer`
pub unsafe fn document_render_pdf(document: *const DocumentInstance) -> Buffer {
    if document.is_null() {
        return Buffer {
            data: ptr::null_mut(),
            len: 0,
        };
    }

    match unsafe { &*document }.render_pdf() {
        Ok(pdf_bytes) => vec_to_buffer(pdf_bytes),
        Err(_) => Buffer {
            data: ptr::null_mut(),
            len: 0,
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::compiler::CompilerInstance;
    use crate::types::CompilerOptions;
    use std::env;
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
    fn test_document_page_count() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("= Page 1\n#pagebreak()\n= Page 2");
        let result = compiler.compile();

        assert!(result.success);
        assert!(!result.document.is_null());

        unsafe {
            let doc = result.document as *const DocumentInstance;
            let count = document_page_count(doc);
            assert_eq!(count, 2);

            // Clean up
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }

    #[test]
    fn test_document_page_count_null() {
        unsafe {
            let count = document_page_count(ptr::null());
            assert_eq!(count, 0);
        }
    }

    #[test]
    fn test_render_single_page_svg() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("= Test Page\n\nContent here.");
        let result = compiler.compile();

        assert!(result.success);

        unsafe {
            let doc = result.document as *const DocumentInstance;
            let svg_buffer = document_render_page_svg(doc, 0);

            assert!(!svg_buffer.data.is_null());
            assert!(svg_buffer.len > 0);

            // Verify it's valid SVG
            let svg_bytes = std::slice::from_raw_parts(svg_buffer.data, svg_buffer.len);
            let svg_str = String::from_utf8_lossy(svg_bytes);
            assert!(svg_str.contains("<svg") || svg_str.starts_with("<?xml"));

            // Clean up
            crate::memory::free_buffer(svg_buffer);
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }

    #[test]
    fn test_render_page_svg_out_of_bounds() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("= Single Page Document");
        let result = compiler.compile();

        assert!(result.success);

        unsafe {
            let doc = result.document as *const DocumentInstance;
            let svg_buffer = document_render_page_svg(doc, 99); // Out of bounds

            // Should return empty buffer on error
            assert!(svg_buffer.data.is_null());
            assert_eq!(svg_buffer.len, 0);

            // Clean up
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }

    #[test]
    fn test_render_page_svg_null_document() {
        unsafe {
            let buffer = document_render_page_svg(ptr::null(), 0);
            assert!(buffer.data.is_null());
            assert_eq!(buffer.len, 0);
        }
    }

    #[test]
    fn test_render_all_pages_svg() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("= Page 1\n#pagebreak()\n= Page 2\n#pagebreak()\n= Page 3");
        let result = compiler.compile();

        assert!(result.success);

        unsafe {
            let doc = result.document as *const DocumentInstance;
            let page_count = document_page_count(doc);
            let array = document_render_all_pages_svg(doc);

            assert!(!array.buffers.is_null());
            assert_eq!(array.len, page_count);

            // Verify each page is valid SVG
            let buffers = std::slice::from_raw_parts(array.buffers, array.len);
            for buffer in buffers {
                assert!(!buffer.data.is_null());
                assert!(buffer.len > 0);

                let svg_bytes = std::slice::from_raw_parts(buffer.data, buffer.len);
                let svg_str = String::from_utf8_lossy(svg_bytes);
                assert!(svg_str.contains("<svg") || svg_str.starts_with("<?xml"));
            }

            // Clean up
            crate::memory::free_buffer_array(array);
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }

    #[test]
    fn test_render_all_pages_svg_null_document() {
        unsafe {
            let array = document_render_all_pages_svg(ptr::null());
            assert!(array.buffers.is_null());
            assert_eq!(array.len, 0);
        }
    }

    #[test]
    fn test_render_pdf() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("= Test");
        let result = compiler.compile();

        assert!(result.success);

        unsafe {
            let doc = result.document as *const DocumentInstance;
            let pdf_buffer = document_render_pdf(doc);

            assert!(!pdf_buffer.data.is_null());
            assert!(pdf_buffer.len > 0);

            // Cleanup
            crate::memory::free_buffer(pdf_buffer);
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }
}
