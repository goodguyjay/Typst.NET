use std::collections::HashMap;
use std::fs;
// ============================================================================
// TYPST IMPORTS - ONLY IN THIS FILE
// ============================================================================
use serde_json::Value as JsonValue;
use std::path::PathBuf;
/// ISOLATION LAYER: This is the ONLY file that import typst types.
/// All typst API interaction happens here. When typst releases a new version,
/// only this file should need to be updated.
use typst::diag::{FileError, FileResult, SourceDiagnostic};
use typst::foundations::{Bytes, Datetime, Dict, Value};
use typst::layout::{Page, PagedDocument};
use typst::syntax::{FileId, Source, VirtualPath};
use typst::text::{Font, FontBook};
use typst::utils::LazyHash;
use typst::{Library, LibraryExt, World};
use typst_kit::fonts::{FontSearcher, Fonts};
use typst_pdf::{PdfOptions, pdf};
use typst_svg::svg;

// TODO: Add PNG export support
// TODO: Add HTML export support (typst::compile::<HtmlDocument>)

// ============================================================================
// INTERNAL TYPES - ABSTRACTION OVER TYPST
// ============================================================================

/// Wrapper around the typst's world implementation
#[derive(Debug)]
pub struct BackendWorld {
    root: PathBuf,
    main_source: Source,
    main_id: FileId,
    fonts: Fonts,
    font_book: LazyHash<FontBook>,
    library: LazyHash<Library>,
    source_cache: HashMap<FileId, Source>,
    binary_cache: HashMap<FileId, Bytes>, // unimplemented for now
    package_path: Option<PathBuf>,
}

/// Wrapper around typst's compiled document
pub struct BackendDocument {
    inner: PagedDocument,
}

/// Internal diagnostic representation
#[derive(Debug, Clone)]
pub struct BackendDiagnostic {
    pub severity: DiagnosticSeverity,
    pub message: String,
    pub location: Option<BackendLocation>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DiagnosticSeverity {
    Error,
    Warning,
}

#[derive(Debug, Clone, Copy)]
pub struct BackendLocation {
    pub line: u32,   // 1-indexed
    pub column: u32, // 1-indexed
    pub length: u32,
}

/// Result of compilation
pub struct BackendCompileResult {
    pub success: bool,
    pub document: Option<BackendDocument>,
    pub diagnostics: Vec<BackendDiagnostic>,
}

// ============================================================================
// WORLD IMPLEMENTATION
// ============================================================================
impl BackendWorld {
    pub fn new(
        root: PathBuf,
        inputs_json: Option<&str>,
        package_path: Option<PathBuf>,
        custom_font_paths: Vec<PathBuf>,
        include_system_fonts: bool,
    ) -> Result<Self, String> {
        // Validate root
        if !root.exists() {
            return Err(format!("Root path does not exist: {}", root.display()));
        }

        if !root.is_dir() {
            return Err(format!("Root path is not a directory: {}", root.display()));
        }

        // Validate package_path if provided
        if let Some(ref pkg_path) = package_path {
            if !pkg_path.exists() {
                return Err(format!(
                    "Package path does not exist: {}",
                    pkg_path.display()
                ));
            }
            if !pkg_path.is_dir() {
                return Err(format!(
                    "Package path is not a directory: {}",
                    pkg_path.display()
                ));
            }
        }

        // Initialize fonts
        let mut searcher = FontSearcher::new();
        searcher.include_system_fonts(include_system_fonts);

        let fonts = searcher.search_with(custom_font_paths);
        let font_book = LazyHash::new(fonts.book.clone());

        // Parse JSON to Dict
        let inputs = if let Some(json) = inputs_json {
            let inputs_val: JsonValue =
                serde_json::from_str(json).map_err(|e| format!("Invalid inputs JSON: {}", e))?;

            match json_to_typst(inputs_val) {
                Value::Dict(d) => d,
                _ => Dict::new(),
            }
        } else {
            Dict::new()
        };

        // Get library w/ inputs
        let library = LazyHash::new(Library::builder().with_inputs(inputs).build());

        // Create empty main source
        let main_id = FileId::new(None, VirtualPath::new("main.typ"));
        let main_source = Source::new(main_id, String::new());

        // Initialize caches
        let source_cache = HashMap::new();
        let binary_cache = HashMap::new();

        Ok(Self {
            root,
            main_source,
            main_id,
            fonts,
            font_book,
            library,
            source_cache,
            binary_cache,
            package_path,
        })
    }

    pub fn update_source(&mut self, source_text: &str) {
        self.main_source = Source::new(self.main_id, source_text.to_string());
    }

    pub fn resolve_path(&self, id: FileId) -> FileResult<PathBuf> {
        match id.package() {
            // The file is a part of a package (@preview, etc.)
            Some(spec) => {
                if let Some(ref pkg_root) = self.package_path {
                    let path = pkg_root
                        .join(&spec.namespace.to_string())
                        .join(&spec.name.to_string())
                        .join(&spec.version.to_string())
                        .join(id.vpath().as_rootless_path());

                    if !path.exists() {
                        return Err(FileError::NotFound(path));
                    }
                    Ok(path)
                } else {
                    // No package path configured, but user tried to use a package
                    Err(FileError::AccessDenied)
                }
            }
            None => {
                let vpath = id.vpath();
                let path = self.root.join(vpath.as_rootless_path());

                // Security check
                let absolute_root = std::path::absolute(&self.root).unwrap_or(self.root.clone());
                let absolute_path = std::path::absolute(&path).unwrap_or(path.clone());

                if !absolute_path.starts_with(&absolute_root) {
                    return Err(FileError::AccessDenied);
                }

                if !path.exists() {
                    return Err(FileError::NotFound(path));
                }
                Ok(path)
            }
        }
    }

    pub fn compile(&mut self) -> BackendCompileResult {
        let warned = typst::compile::<PagedDocument>(self);

        // Extract diagnostics (warnings always present)
        let warnings = warned
            .warnings
            .iter()
            .map(|diag| convert_diagnostic(diag, self))
            .collect();

        match warned.output {
            Ok(document) => BackendCompileResult {
                success: true,
                document: Some(BackendDocument { inner: document }),
                diagnostics: warnings,
            },
            Err(errors) => {
                let mut all_diagnostics: Vec<BackendDiagnostic> = errors
                    .iter()
                    .map(|diag| convert_diagnostic(diag, self))
                    .collect();
                all_diagnostics.extend(warnings);

                BackendCompileResult {
                    success: false,
                    document: None,
                    diagnostics: all_diagnostics,
                }
            }
        }
    }
}

impl World for BackendWorld {
    fn library(&self) -> &LazyHash<Library> {
        &self.library
    }

    fn book(&self) -> &LazyHash<FontBook> {
        &self.font_book
    }

    fn main(&self) -> FileId {
        self.main_id
    }

    fn source(&self, id: FileId) -> FileResult<Source> {
        // Check if this is the main source
        if id == self.main_id {
            return Ok(self.main_source.clone());
        };

        // Check cache first
        if let Some(source) = self.source_cache.get(&id) {
            return Ok(source.clone());
        }

        // Otherwise, it's an external typ file from filesystem
        let path = self.resolve_path(id)?;
        let text = fs::read_to_string(&path).map_err(|e| FileError::from_io(e, &path))?;
        let source = Source::new(id, text);

        // in the future we'll insert into cache here,
        // but since self is &self, we'll need interior mutability (RefCell/DashMap)
        // but for now, just reading is fine
        Ok(source)
    }

    fn file(&self, id: FileId) -> FileResult<Bytes> {
        let path = self.resolve_path(id)?;

        let bytes_vec = fs::read(&path).map_err(|err| FileError::from_io(err, &path))?;

        Ok(Bytes::new(bytes_vec))
    }

    fn font(&self, index: usize) -> Option<Font> {
        self.fonts.fonts.get(index).and_then(|slot| slot.get())
    }

    fn today(&self, offset: Option<i64>) -> Option<Datetime> {
        use time::OffsetDateTime;

        if let Some(offset_hours) = offset {
            // Apply UTC offset in hours
            let offset = time::UtcOffset::from_hms(offset_hours as i8, 0, 0).ok()?;
            let now = OffsetDateTime::now_utc().to_offset(offset);

            Datetime::from_ymd_hms(
                now.year(),
                now.month() as u8,
                now.day(),
                now.hour(),
                now.minute(),
                now.second(),
            )
        } else {
            // Local time (auto offset)
            let now = OffsetDateTime::now_local().ok()?;

            Datetime::from_ymd_hms(
                now.year(),
                now.month() as u8,
                now.day(),
                now.hour(),
                now.minute(),
                now.second(),
            )
        }
    }
}

// ============================================================================
// DOCUMENT RENDERING
// ============================================================================

impl BackendDocument {
    pub fn page_count(&self) -> usize {
        self.inner.pages.len()
    }

    /// Render a single page to SVG
    pub fn render_page_svg(&self, page_index: usize) -> Result<Vec<u8>, String> {
        if page_index >= self.inner.pages.len() {
            return Err(format!(
                "Page index {} out of bounds (document has {} pages)",
                page_index,
                self.inner.pages.len()
            ));
        }

        let page: &Page = &self.inner.pages[page_index];
        let svg_string = svg(page);

        Ok(svg_string.into_bytes())
    }

    /// Render all pages to SVG
    pub fn render_all_pages_svg(&self) -> Result<Vec<Vec<u8>>, String> {
        if self.inner.pages.is_empty() {
            return Err("Document has no pages to render".to_string());
        }

        let mut results = Vec::with_capacity(self.inner.pages.len());

        for page in &self.inner.pages {
            let svg_string = svg(page);
            results.push(svg_string.into_bytes());
        }

        Ok(results)
    }

    /// Render entire document to PDF
    pub fn render_pdf(&self) -> Result<Vec<u8>, String> {
        let options = PdfOptions::default();

        match pdf(&self.inner, &options) {
            Ok(bytes) => Ok(bytes.into()),
            Err(errors) => {
                let error_msg = errors
                    .iter()
                    .map(|e| e.message.to_string())
                    .collect::<Vec<_>>()
                    .join("; ");
                Err(format!("PDF rendering failed: {}", error_msg))
            }
        }
    }
}

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

/// Converts typst's SourceDiagnostic to our BackendDiagnostic
fn convert_diagnostic(diag: &SourceDiagnostic, world: &BackendWorld) -> BackendDiagnostic {
    let severity = match diag.severity {
        typst::diag::Severity::Error => DiagnosticSeverity::Error,
        typst::diag::Severity::Warning => DiagnosticSeverity::Warning,
    };

    // Format message including hints
    let mut message = diag.message.to_string();
    for hint in &diag.hints {
        message.push_str("\nHint: ");
        message.push_str(&hint.to_string());
    }

    let mut location = None;
    let span = diag.span;

    if let Some(id) = span.id() {
        if let Ok(source) = world.source(id) {
            if let Some(range) = source.range(span) {
                let lines = source.lines();

                // Note: Typst indices are 0-based; .NET is 1-based.
                let line = lines
                    .byte_to_line(range.start)
                    .map(|l| l as u32 + 1)
                    .unwrap_or(0);
                let col = lines
                    .byte_to_column(range.start)
                    .map(|c| c as u32 + 1)
                    .unwrap_or(0);
                let length = (range.end - range.start) as u32;

                if line > 0 {
                    location = Some(BackendLocation {
                        line,
                        column: col,
                        length,
                    });
                }
            }
        }
    }

    BackendDiagnostic {
        severity,
        message,
        location,
    }
}

/// Converts serde_json::Value to typst::Value recursively
fn json_to_typst(json: JsonValue) -> Value {
    match json {
        JsonValue::Null => Value::None,
        JsonValue::Bool(b) => Value::Bool(b),
        JsonValue::Number(n) => {
            if let Some(i) = n.as_i64() {
                Value::Int(i)
            } else {
                Value::Float(n.as_f64().unwrap_or(0.0))
            }
        }
        JsonValue::String(s) => Value::Str(s.into()),
        JsonValue::Array(a) => Value::Array(a.into_iter().map(json_to_typst).collect()),
        JsonValue::Object(o) => {
            let mut dict = Dict::new();
            for (k, v) in o {
                dict.insert(k.into(), json_to_typst(v));
            }
            Value::Dict(dict)
        }
    }
}

// ============================================================================
// VERSION COMPATIBILITY
// ============================================================================

/// Compile-time check that we're using the expected typst version.
#[cfg(test)]
mod version_tests {
    /// This will be removed later.
    #[test]
    fn verify_typst_version() {
        const EXPECTED_VERSION: &str = "0.14.2";
        const ACTUAL_VERSION: &str = env!("TYPST_VERSION");

        assert_eq!(
            ACTUAL_VERSION, EXPECTED_VERSION,
            "typst version changed! expected {}, found {}. update typst_backend.rs if needed.",
            EXPECTED_VERSION, ACTUAL_VERSION
        );
    }

    #[test]
    fn verify_compile_signature() {
        // compile-time check: this won't compile if typst::compile signature changes
        fn _assert_compile_signature<W: typst::World>(world: &W) {
            let _: typst::diag::Warned<typst::diag::SourceResult<typst::layout::PagedDocument>> =
                typst::compile::<typst::layout::PagedDocument>(world);
        }
    }
}

#[cfg(test)]
mod vfs_tests {
    use super::*;
    use std::env;
    use std::fs;

    #[test]
    fn test_file_read_simple() {
        let temp_dir = env::temp_dir().join("typst_vfs_test");
        fs::create_dir_all(&temp_dir).unwrap();

        let test_file = temp_dir.join("data.txt");
        fs::write(&test_file, b"Hello, VFS!").unwrap();

        let mut world = BackendWorld::new(temp_dir.clone(), None, None, vec![], true).unwrap();

        world.update_source(
            r#"#let data = read("data.txt")
                          Data: #data"#,
        );

        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(temp_dir).ok();

        assert!(result.success, "Compilation should succeed")
    }

    #[test]
    fn test_file_not_found() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        // Try to read non-existent file
        world.update_source(r#"#read("doesnotexist.txt")"#);
        let result = world.compile();

        // Should fail
        assert!(!result.success);
        assert!(!result.diagnostics.is_empty());
    }

    #[test]
    fn test_import_statement() {
        let temp_dir = env::temp_dir().join("typst_import_test");
        fs::create_dir_all(&temp_dir).unwrap();

        let module_file = temp_dir.join("helper.typ");
        fs::write(&module_file, b"#let greet(name) = \"Hello, \" + name").unwrap();

        let mut world = BackendWorld::new(temp_dir.clone(), None, None, vec![], true).unwrap();

        // Compile with import
        world.update_source(
            r#"#import "helper.typ": greet
                        #greet("World")"#,
        );

        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(&temp_dir).ok();

        assert!(result.success, "Import should work");
    }

    #[test]
    fn test_read_json() {
        let temp_dir = env::temp_dir().join("typst_json_test");
        fs::create_dir_all(&temp_dir).unwrap();

        // Create JSON file
        let json_file = temp_dir.join("data.json");
        fs::write(&json_file, br#"{"name": "Test", "value": 42}"#).unwrap();

        // Create world
        let mut world = BackendWorld::new(temp_dir.clone(), None, None, vec![], true).unwrap();

        // Read and parse JSON
        world.update_source(
            r#"#let data = json("data.json")
                        Name: #data.name
                        Value: #data.value"#,
        );

        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(&temp_dir).ok();

        assert!(result.success, "JSON read should work");
    }

    #[test]
    fn test_nested_directory_import() {
        let temp_dir = env::temp_dir().join("typst_nested_test");
        fs::create_dir_all(&temp_dir.join("components")).unwrap();

        // Create nested file
        let component = temp_dir.join("components/header.typ");
        fs::write(&component, b"#let header = \"My Header\"").unwrap();

        let mut world = BackendWorld::new(temp_dir.clone(), None, None, vec![], true).unwrap();

        // Import from subdirectory
        world.update_source(
            r#"#import "components/header.typ": header
                       = #header"#,
        );

        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(&temp_dir).ok();

        assert!(result.success, "Nested import should work");
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::env;

    #[test]
    fn test_backend_world_creation() {
        let temp_dir = env::temp_dir();
        let world = BackendWorld::new(temp_dir, None, None, vec![], true);

        assert!(world.is_ok());
    }

    #[test]
    fn test_backend_world_with_inputs() {
        let temp_dir = env::temp_dir();
        let inputs_json = r#"{"key": "value", "number": "42"}"#;
        let world = BackendWorld::new(temp_dir, Some(inputs_json), None, vec![], true);
        assert!(world.is_ok());
    }

    #[test]
    fn test_backend_world_invalid_path() {
        let invalid_path = PathBuf::from("/path/that/does/not/exist");
        let world = BackendWorld::new(invalid_path, None, None, vec![], true);

        assert!(world.is_err());
        assert!(world.unwrap_err().contains("does not exist"));
    }

    #[test]
    fn test_backend_world_file_not_directory() {
        // try to use a file as root (should fail)
        let file_path = env::current_exe().unwrap();
        let world = BackendWorld::new(file_path, None, None, vec![], true);

        assert!(world.is_err());
        assert!(world.unwrap_err().contains("not a directory"));
    }

    #[test]
    fn test_simple_compilation_success() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        world.update_source("= Hello World\n\nThis is a test.");
        let result = world.compile();

        assert!(result.success);
        assert!(result.document.is_some());

        let doc = result.document.unwrap();
        assert!(doc.page_count() > 0);
    }

    #[test]
    fn test_compilation_error() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        // invalid syntax
        world.update_source("#let x = (unclosed");
        let result = world.compile();

        assert!(!result.success);
        assert!(result.document.is_none());
        assert!(!result.diagnostics.is_empty());

        // Should have at least one error
        let has_error = result
            .diagnostics
            .iter()
            .any(|d| matches!(d.severity, DiagnosticSeverity::Error));
        assert!(has_error);
    }

    #[test]
    fn test_svg_rendering_single_page() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        world.update_source("= Test page\n\nContent here.");
        let result = world.compile();

        assert!(result.success);
        let doc = result.document.unwrap();

        // Render fist page
        let svg = doc.render_page_svg(0);
        assert!(svg.is_ok());

        let svg_bytes = svg.unwrap();
        assert!(!svg_bytes.is_empty());

        // Should be valid SVG (starts with XML declaration or <svg)
        let svg_str = String::from_utf8_lossy(&svg_bytes);
        assert!(svg_str.contains("<svg") || svg_str.starts_with("<?xml"));
    }

    #[test]
    fn test_svg_rendering_all_pages() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        world.update_source("= Page 1\n#pagebreak()\n= Page 2\n#pagebreak()\n= Page 3");
        let result = world.compile();

        assert!(result.success);
        let doc = result.document.unwrap();

        // Render all pages
        let svgs = doc.render_all_pages_svg();
        assert!(svgs.is_ok());

        let svg_pages = svgs.unwrap();
        assert_eq!(svg_pages.len(), doc.page_count());

        // Each page should be valid SVG
        for svg_bytes in svg_pages {
            assert!(!svg_bytes.is_empty());

            let svg_str = String::from_utf8_lossy(&svg_bytes);
            assert!(svg_str.contains("<svg") || svg_str.starts_with("<?xml"));
        }
    }

    #[test]
    fn test_diagnostic_formatting() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        // Introduce a warning (unused variable)
        world.update_source("#let unused = 5\n= Title");
        let result = world.compile();

        // Should compile successfully but with a warning
        assert!(result.success);

        // Check diagnostic structure
        for diag in &result.diagnostics {
            assert!(!diag.message.is_empty());
            // Severity should be valid
            assert!(matches!(
                diag.severity,
                DiagnosticSeverity::Error | DiagnosticSeverity::Warning
            ));
        }
    }

    #[test]
    fn test_update_source() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        // First compilation
        world.update_source("= First");
        let result1 = world.compile();
        assert!(result1.success);

        // Update source and recompile
        world.update_source("= Second\nMore content.");
        let result2 = world.compile();
        assert!(result2.success);

        // Both should succeed independently
        assert!(result1.document.is_some());
        assert!(result2.document.is_some());
    }

    #[test]
    fn test_fonts_loaded() {
        let temp_dir = env::temp_dir();
        let world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        // Should have some fonts available
        assert!(!world.fonts.fonts.is_empty());

        // Should be able to get a font
        let font = world.font(0);
        assert!(font.is_some());
    }

    #[test]
    fn test_world_without_system_fonts() {
        let temp_dir = env::temp_dir();
        let world = BackendWorld::new(temp_dir, None, None, vec![], false);

        // Should still succeed (embedded fonts available)
        assert!(world.is_ok());

        let world = world.unwrap();
        // Should still have embedded fonts
        assert!(!world.fonts.fonts.is_empty());
    }
}

#[cfg(test)]
mod pdf_tests {
    use super::*;
    use std::env;

    #[test]
    fn test_pdf_rendering_basic() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        world.update_source("= PDF Test\n\nContent here.");
        let result = world.compile();

        assert!(result.success);
        let doc = result.document.unwrap();

        let pdf_bytes = doc.render_pdf().unwrap();
        assert!(!pdf_bytes.is_empty());

        // PDF should start with %PDF-
        assert_eq!(&pdf_bytes[0..5], b"%PDF-");
    }

    #[test]
    fn test_pdf_multipage() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(temp_dir, None, None, vec![], true).unwrap();

        world.update_source("= Page 1\n#pagebreak()\n= Page 2");
        let result = world.compile();

        assert!(result.success);
        let doc = result.document.unwrap();

        let pdf_bytes = doc.render_pdf().unwrap();
        assert!(pdf_bytes.len() > 1000); // Should be larger than 1KB
    }
}

#[cfg(test)]
mod package_tests {
    use super::*;
    use std::env;
    use std::fs;

    #[test]
    fn test_package_import_with_path() {
        // Mock package structure
        let temp_dir = env::temp_dir().join("typst_package_test");
        let package_dir = temp_dir.join("packages");
        let preview_dir = package_dir.join("preview");
        let pkg_name_dir = preview_dir.join("mylib");
        let pkg_version_dir = pkg_name_dir.join("0.1.0");

        fs::create_dir_all(&pkg_version_dir).unwrap();

        let toml_file = pkg_version_dir.join("typst.toml");
        let toml_content = r#"
            [package]
            name = "mylib"
            version = "0.1.0"
            entrypoint = "lib.typ"
        "#;

        fs::write(&toml_file, toml_content).unwrap();

        let lib_file = pkg_version_dir.join("lib.typ");
        fs::write(&lib_file, b"#let hello = \"Hello from package!\"").unwrap();

        let workspace = temp_dir.join("workspace");
        fs::create_dir_all(&workspace).unwrap();

        let mut world =
            BackendWorld::new(workspace, None, Some(package_dir.clone()), vec![], true).unwrap();

        world.update_source(
            r#"#import "@preview/mylib:0.1.0": hello
                                             #hello"#,
        );

        let result = world.compile();

        if !result.success {
            for diag in &result.diagnostics {
                println!("Error: {}", diag.message);
            }
        }

        // Cleanup
        fs::remove_dir_all(&temp_dir).ok();

        assert!(result.success, "Package import should work");
    }

    #[test]
    fn test_package_not_found() {
        let temp_dir = env::temp_dir();
        let package_dir = temp_dir.join("empty_packages");
        fs::create_dir_all(&package_dir).unwrap();

        let mut world = BackendWorld::new(
            temp_dir.clone(),
            None,
            Some(package_dir.clone()),
            vec![],
            true,
        )
        .unwrap();

        // Try to import non-existent package
        world.update_source(r#"#import "@preview/doesnotexist:0.1.0": *"#);
        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(&package_dir).ok();

        // Should fail
        assert!(!result.success);
        assert!(!result.diagnostics.is_empty());
    }

    #[test]
    fn test_package_without_path_configured() {
        let temp_dir = env::temp_dir();
        let mut world = BackendWorld::new(
            temp_dir,
            None,
            None, // No package path
            vec![],
            true,
        )
        .unwrap();

        // Try to import package without path configured
        world.update_source(r#"#import "@preview/test:0.1.0": *"#);
        let result = world.compile();

        // Should fail with meaningful error
        assert!(!result.success);
        assert!(!result.diagnostics.is_empty());
    }

    #[test]
    fn test_package_nested_files() {
        let temp_dir = env::temp_dir().join("typst_nested_package_test");
        let package_dir = temp_dir.join("packages");
        let pkg_base_dir = package_dir.join("preview").join("utils");
        let pkg_version_dir = pkg_base_dir.join("1.0.0");
        let src_dir = pkg_version_dir.join("src");

        fs::create_dir_all(&src_dir).unwrap();
        fs::create_dir_all(&pkg_version_dir).unwrap();

        let toml_file = pkg_version_dir.join("typst.toml");
        let toml_content = r#"
            [package]
            name = "utils"
            version = "1.0.0"
            entrypoint = "lib.typ"
        "#;

        fs::write(&toml_file, toml_content).unwrap();

        // Create nested files
        fs::write(
            pkg_version_dir.join("lib.typ"),
            b"#import \"src/helpers.typ\": *",
        )
        .unwrap();

        fs::write(
            src_dir.join("helpers.typ"),
            b"#let greet = \"Hello from nested file!\"",
        )
        .unwrap();

        let workspace = temp_dir.join("workspace");
        fs::create_dir_all(&workspace).unwrap();

        let mut world =
            BackendWorld::new(workspace, None, Some(package_dir.clone()), vec![], true).unwrap();

        // Import package that imports nested file
        world.update_source(
            r#"#import "@preview/utils:1.0.0": greet
                        #greet"#,
        );

        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(&temp_dir).ok();

        assert!(result.success, "Nested package imports should work");
    }

    #[test]
    fn test_package_path_security() {
        let temp_dir = env::temp_dir().join("typst_security_test");
        let package_dir = temp_dir.join("packages");
        fs::create_dir_all(&package_dir).unwrap();

        // Create a file OUTSIDE package directory
        let secret_file = temp_dir.join("secret.typ");
        fs::write(&secret_file, b"#let secret = \"LEAKED!\"").unwrap();

        let workspace = temp_dir.join("workspace");
        fs::create_dir_all(&workspace).unwrap();

        let mut world =
            BackendWorld::new(workspace, None, Some(package_dir.clone()), vec![], true).unwrap();

        // Try path traversal attack
        world.update_source(r#"#import "@preview/../secret:0.0.0": *"#);
        let result = world.compile();

        // Cleanup
        fs::remove_dir_all(&temp_dir).ok();

        // Should fail - cannot escape package directory
        assert!(!result.success);
    }
}

#[cfg(test)]
mod font_tests {
    use super::*;
    use std::env;

    #[test]
    fn test_custom_font_paths_no_crash() {
        let temp_dir = env::temp_dir().join("typst_font_test");
        fs::create_dir_all(&temp_dir).unwrap();

        let world = BackendWorld::new(
            env::current_dir().unwrap(),
            None,
            None,
            vec![temp_dir.clone()],
            false, // Only use custom paths, not system fonts
        );

        fs::remove_dir_all(&temp_dir).ok();

        assert!(world.is_ok(), "Should create world with custom font path");
    }

    #[test]
    fn test_multiple_font_paths() {
        let temp1 = env::temp_dir().join("fonts1");
        let temp2 = env::temp_dir().join("fonts2");
        fs::create_dir_all(&temp1).unwrap();
        fs::create_dir_all(&temp2).unwrap();

        let world = BackendWorld::new(
            env::current_dir().unwrap(),
            None,
            None,
            vec![temp1.clone(), temp2.clone()],
            true,
        );

        fs::remove_dir_all(&temp1).ok();
        fs::remove_dir_all(&temp2).ok();

        assert!(world.is_ok());
    }
}
