# Fallout 4 Data Collection Requirements

## Critical Data Needed from Community

### 1. Executable SHA256 Hashes
Need to collect verified hashes for:
- **Fallout4.exe v1.10.163.0** (Pre-Next Gen)
  - Steam version
  - GOG version (if different)
  - Verify across multiple installations
  
- **Fallout4.exe v1.10.984.0** (Next Gen Update)
  - Steam version
  - GOG version (if different)
  - Epic Games version (if different)

### 2. F4SE File Hashes
For version validation:
- **F4SE 0.6.23** (for game v1.10.163.0)
  - f4se_1_10_163.dll
  - f4se_loader.exe
  - f4se_steam_loader.dll
  
- **F4SE 0.7.2** (for game v1.10.984.0)
  - f4se_1_10_984.dll
  - f4se_loader.exe
  - f4se_steam_loader.dll

### 3. Core Framework Mod Hashes
Essential mods that should be validated:
- **Buffout 4** (various versions)
  - Buffout4.dll
  - Buffout4.toml (default config)
  
- **Address Library**
  - version-1-10-163-0.bin
  - version-1-10-984-0.bin
  
- **Mod Configuration Menu**
  - MCM.dll
  - Interface files

### 4. Critical Game Files
Files that often get corrupted/modified:
- **Data\Fallout4.esm** (master file)
- **Data\Fallout4 - Textures1.ba2**
- **Data\Scripts\*.pex** (vanilla script files)

### 5. INI File Defaults
Need clean versions of:
- Fallout4.ini
- Fallout4Prefs.ini
- Fallout4Custom.ini (structure)

## Collection Methodology

### Hash Generation Script
```python
import hashlib
import os

def generate_sha256(filepath):
    """Generate SHA256 hash for a file"""
    sha256_hash = hashlib.sha256()
    with open(filepath, "rb") as f:
        for byte_block in iter(lambda: f.read(4096), b""):
            sha256_hash.update(byte_block)
    return sha256_hash.hexdigest()

# Example usage
game_exe = r"C:\Steam\steamapps\common\Fallout 4\Fallout4.exe"
print(f"SHA256: {generate_sha256(game_exe)}")
```

### Verification Process
1. Collect from multiple sources (5+ users minimum)
2. Compare hashes to ensure consistency
3. Document any platform-specific differences
4. Note file sizes along with hashes

## YAML Structure for Data Storage

```yaml
fallout4_file_hashes:
  executables:
    1.10.163.0:
      steam: "actual_hash_here"
      gog: "actual_hash_here"
      file_size: 102400000
    1.10.984.0:
      steam: "actual_hash_here"
      gog: "actual_hash_here"
      file_size: 104857600
      
  f4se_files:
    0.6.23:
      f4se_1_10_163.dll: "hash"
      f4se_loader.exe: "hash"
    0.7.2:
      f4se_1_10_984.dll: "hash"
      f4se_loader.exe: "hash"
      
  core_mods:
    buffout4:
      version: "1.28.6"
      files:
        Buffout4.dll: "hash"
        
  vanilla_scripts:
    Actor.pex: "hash"
    ObjectReference.pex: "hash"
    # ... more critical scripts
```

## Community Sources

### Reliable Hash Sources
- Nexus Mods verified files
- F4SE official releases
- Steam Database (SteamDB)
- Community wikis
- Trusted modding Discord servers

### Validation Partners
Consider reaching out to:
- Buffout 4 author (for crash log format verification)
- F4SE team (for version compatibility matrix)
- Major modding framework authors
- Wabbajack modlist curators

## Testing Requirements

### Minimum Test Coverage
- Fresh Steam installation
- Steam with Next Gen update
- Downgraded installation (1.10.984 → 1.10.163)
- Heavily modded installation
- GOG installation (if available)

### Edge Cases to Test
- Pirated versions (for detection, not support)
- Modified executables (ENB, LAA patches)
- Incomplete updates
- Corrupted installations

## Documentation Needs

### For Each File Hash
- File path relative to game root
- File size in bytes
- SHA256 hash
- Platform/version information
- Date collected
- Source of hash

This data collection will ensure accurate version detection and provide a solid foundation for the FCX implementation.