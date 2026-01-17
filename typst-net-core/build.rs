use std::{env, fs};
use std::path::Path;

fn main() {
    let manifest_dir = env::var("CARGO_MANIFEST_DIR").unwrap();
    let cargo_toml_path = Path::new(&manifest_dir).join("Cargo.toml");
    let cargo_toml = fs::read_to_string(&cargo_toml_path).unwrap();

    // Find typst dep version
    let typst_version = cargo_toml
        .lines()
        .find(|line| line.trim().starts_with("typst ="))
        .and_then(|line| {
            line.split('=')
                .nth(1)?
                .trim()
                .trim_matches(|c| c == '"' || c == '\'' || c == ' ')
                .split_whitespace()
                .next()
        })
        .unwrap_or("unknown");

    println!("cargo:rustc-env=TYPST_VERSION={}", typst_version);
    println!("cargo:rerun-if-changed=Cargo.toml");
}