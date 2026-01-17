// High-level compiler logic using internal types only.
// No direct typst imports. Everything goes through typst_backend.

use crate::memory::{create_diagnostic, diagnostics_to_array};
use crate::types::{CompileResult, CompilerOptions, Diagnostic, DiagnosticSeverity};
use crate::typst_backend::{BackendCompileResult, BackendDocument, BackendWorld};
use std::path::PathBuf;
use std::ptr;

pub struct CompilerInstance {
    world: BackendWorld,
}

impl CompilerInstance {
    /// Create a new compiler instance
    ///
    /// Parses all options and create world.
    /// Does NOT retain any pointers from options.
    pub fn new(root: PathBuf, options: &CompilerOptions) -> Result<Self, String> {
        // Parse inputs from JSON (or empty dict)
        let inputs = Self::parse_inputs(options)?;

        let package_path = if !options.package_path.is_null() && options.package_path_len > 0 {
            unsafe {
                let path_bytes =
                    std::slice::from_raw_parts(options.package_path, options.package_path_len);
                let path_str =
                    std::str::from_utf8(path_bytes).map_err(|_| "Invalid UTF-8 in package path")?;
                Some(PathBuf::from(path_str))
            }
        } else {
            None
        };
        
        let custom_font_paths = Self::parse_custom_font_paths(options)?;

        let world = BackendWorld::new(
            root,
            inputs.as_deref(),
            package_path,
            custom_font_paths,
            options.include_system_fonts,
        )?;

        Ok(Self { world })
    }

    /// Update the source code to compile
    pub fn update_source(&mut self, source: &str) {
        self.world.update_source(source);
    }

    /// Compile the current source
    pub fn compile(&mut self) -> CompileResult {
        let backend_result: BackendCompileResult = self.world.compile();

        // Convert backend diagnostics to FFI diagnostics
        let diagnostics = backend_result
            .diagnostics
            .into_iter()
            .map(convert_backend_diagnostic)
            .collect();

        let (diagnostics_ptr, diagnostics_len) = diagnostics_to_array(diagnostics);

        // Convert the document if present
        let document_ptr = if let Some(backend_doc) = backend_result.document {
            Box::into_raw(Box::new(DocumentInstance::new(backend_doc))) as *mut std::ffi::c_void
        } else {
            ptr::null_mut()
        };

        CompileResult {
            success: backend_result.success,
            diagnostics: diagnostics_ptr,
            diagnostics_len,
            document: document_ptr,
        }
    }

    /// Parse inputs JSON
    fn parse_inputs(options: &CompilerOptions) -> Result<Option<String>, String> {
        if options.inputs_json.is_null() || options.inputs_json_len == 0 {
            return Ok(None);
        }

        unsafe {
            let json_bytes =
                std::slice::from_raw_parts(options.inputs_json, options.inputs_json_len);

            let json_str =
                std::str::from_utf8(json_bytes).map_err(|_| "Invalid UTF-8 in inputs JSON")?;

            // Validate it's valid JSON (but don't parse to Dict here)
            serde_json::from_str::<serde_json::Value>(json_str)
                .map_err(|e| format!("Invalid inputs JSON: {}", e))?;

            Ok(Some(json_str.to_string()))
        }
    }
    
    /// Parse custom font paths from JSON array
    fn parse_custom_font_paths(options: &CompilerOptions) -> Result<Vec<PathBuf>, String> {
        if options.custom_font_paths.is_null() || options.custom_font_paths_len == 0 {
            return Ok(Vec::new());
        }
        
        unsafe {
            let json_bytes = std::slice::from_raw_parts(
                options.custom_font_paths,
                options.custom_font_paths_len,
            );

            let json_str = std::str::from_utf8(json_bytes)
                .map_err(|_| "Invalid UTF-8 in custom font paths")?;

            // Parse as JSON array of strings
            let paths: Vec<String> = serde_json::from_str(json_str)
                .map_err(|e| format!("Invalid font paths JSON: {}", e))?;

            Ok(paths.into_iter().map(PathBuf::from).collect())
        }
    }
}

/// Internal representation of a document instance
pub struct DocumentInstance {
    backend_doc: BackendDocument,
}

impl DocumentInstance {
    /// Create a new document instance from backend document
    pub fn new(backend_doc: BackendDocument) -> Self {
        Self { backend_doc }
    }

    /// Get the number of pages in the document
    pub fn page_count(&self) -> usize {
        self.backend_doc.page_count()
    }

    /// Render a single page to SVG
    pub fn render_page_svg(&self, page_index: usize) -> Result<Vec<u8>, String> {
        self.backend_doc.render_page_svg(page_index)
    }

    /// Render all pages to SVG
    pub fn render_all_pages_svg(&self) -> Result<Vec<Vec<u8>>, String> {
        self.backend_doc.render_all_pages_svg()
    }

    /// Render document to PDF
    pub fn render_pdf(&self) -> Result<Vec<u8>, String> {
        self.backend_doc.render_pdf()
    }
}

/// Convert backend diagnostic to FFI diagnostic
fn convert_backend_diagnostic(backend_diag: crate::typst_backend::BackendDiagnostic) -> Diagnostic {
    let severity = match backend_diag.severity {
        crate::typst_backend::DiagnosticSeverity::Error => DiagnosticSeverity::Error,
        crate::typst_backend::DiagnosticSeverity::Warning => DiagnosticSeverity::Warning,
    };

    let location = backend_diag
        .location
        .map(|loc| (loc.line, loc.column, loc.length));

    create_diagnostic(severity, backend_diag.message, location)
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
    fn test_compiler_creation() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let compiler = CompilerInstance::new(temp_dir, &options);

        assert!(compiler.is_ok());
    }

    #[test]
    fn test_compiler_invalid_path() {
        let invalid_path = PathBuf::from("/non/existent/path/for/testing");
        let options = default_options();
        let compiler = CompilerInstance::new(invalid_path, &options);

        assert!(compiler.is_err());
    }

    #[test]
    fn test_compile_simple_document() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("= Hello World\n\nTest content.");
        let result = compiler.compile();

        assert!(result.success);
        assert!(!result.document.is_null());

        // Clean up
        unsafe {
            if !result.document.is_null() {
                let _ = Box::from_raw(result.document as *mut DocumentInstance);
            }
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }

    #[test]
    fn test_compile_with_errors() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        compiler.update_source("#let x = (unclosed");
        let result = compiler.compile();

        assert!(!result.success);
        assert!(result.document.is_null());
        assert!(result.diagnostics_len > 0);

        // Clean up
        unsafe {
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }

    #[test]
    fn test_multiple_compilations() {
        let temp_dir = env::temp_dir();
        let options = default_options();
        let mut compiler = CompilerInstance::new(temp_dir, &options).unwrap();

        // First compilation
        compiler.update_source("= First");
        let result1 = compiler.compile();
        assert!(result1.success);

        // Second compilation with different source
        compiler.update_source("= Second\n\nNew content.");
        let result2 = compiler.compile();
        assert!(result2.success);

        // Clean up
        unsafe {
            if !result1.document.is_null() {
                let _ = Box::from_raw(result1.document as *mut DocumentInstance);
            }
            if !result2.document.is_null() {
                let _ = Box::from_raw(result2.document as *mut DocumentInstance);
            }
            crate::memory::free_diagnostics(result1.diagnostics, result1.diagnostics_len);
            crate::memory::free_diagnostics(result2.diagnostics, result2.diagnostics_len);
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
            let doc = &*(result.document as *const DocumentInstance);
            assert_eq!(doc.page_count(), 2);

            // Clean up
            let _ = Box::from_raw(result.document as *mut DocumentInstance);
            crate::memory::free_diagnostics(result.diagnostics, result.diagnostics_len);
        }
    }
}
