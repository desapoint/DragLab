# L1D R1 reserved-build values: pre-edit source provenance

Status: pre-edit freeze for the bounded new-value/import/Editor-checker implementation approved in `8aca0e2`. Existing business, equality, load, schema and query methods remain unchanged. No runtime/build pass is claimed.

Full pre-edit Game/Packages/ProjectSettings/tools manifest: `S06D2B1L1D/source-reservation-before-sha256.txt`, 2,171 paths, SHA-256 `CD730FB786E80B0C18D422AEA536A3DE6D569D20FF2F10C62CB53B22B9B28F36`. Root freeze/Editor-player inventory process exited 0 with no running Editor/player. This full raw manifest remains local; the existing required Domain266/reference28 fixtures stay versioned.

Declaration hashes below are normalized LF UTF-8 declarations without final newline. Exact file bytes are independently checked; no methods are authorized for editing. The Editor checker embeds an independent frozen reservation hash/text/decoded-comparison reference. The pure candidate never calls that reference, JSON or SnapshotEquality. Import-only serialization is isolated in its named Infrastructure file.

| Existing method | Path | Lines | Declaration SHA-256 | File SHA-256 |
|---|---|---:|---|---|
| `DragLab.Domain.AwdSpec.Copy()` | Game/Assets/DragLab/Scripts/Domain/AxleDriveDynamics.cs | 17–17 | C6600BCA2EDE7A01F694B03E9F3BE3948BCDC72662001DB4DFC9DA511144ED1C | CA5037B3AAF228C6A15C064249D568CA49F2947985862A11D9E12C918BD9DB9B |
| `DragLab.Domain.Tune.Copy()` | Game/Assets/DragLab/Scripts/Domain/DragSimulation.cs | 251–251 | 07EBBD1B1E0A027ECA0B4D6FC294DF8D7B37C290CD939E03ED014CE022B1329D | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| `DragLab.Domain.Mechanics.EngineElement.Copy()` | Game/Assets/DragLab/Scripts/Domain/EngineElements.cs | 14–14 | F62526D534CD39AAAABB543F62CAC3914DC9113FAB8FEFACA3615E3C12569AE6 | F7F2BC9204CCA51C5EB96E08DC0A868786C702A7D455D7ADB7D2E73B82073EDE |
| `DragLab.Domain.Mechanics.GarageBusiness.Validate(DragLab.Domain.PlayerProfile)` | Game/Assets/DragLab/Scripts/Domain/GarageBusiness.cs | 104–109 | DC8981E24B5044C8BC49BC71D8FBD2964101257C2BF074A1C5C28D48A8F2BF76 | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| `DragLab.Domain.Mechanics.GarageBusiness.Reserved(DragLab.Domain.PlayerProfile, string)` | Game/Assets/DragLab/Scripts/Domain/GarageBusiness.cs | 110–110 | BD1610A0CAC001F138043406E3A575DDFE90E282E0EE61F4B485749CA06296CE | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| `DragLab.Domain.Mechanics.GarageBusiness.Snapshot(DragLab.Domain.PlayerProfile, DragLab.Domain.OwnedCar)` | Game/Assets/DragLab/Scripts/Domain/GarageBusiness.cs | 82–87 | 29F747BC68DB19F44C0BAE3E632EA6C2BF62AB2B347D497A8E3C922EA171F6E0 | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| `DragLab.Domain.Mechanics.GarageBusiness.Contents(DragLab.Domain.PlayerProfile, DragLab.Domain.OwnedCar)` | Game/Assets/DragLab/Scripts/Domain/GarageBusiness.cs | 88–88 | 5354B07A52EB947D898A514B032D99A945DA895D3936FF7551B01753D1A87CED | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| `DragLab.Domain.Mechanics.GarageBusiness.Matches(DragLab.Domain.PlayerProfile, DragLab.Domain.OwnedCar, DragLab.Domain.Mechanics.AssignedGarageJob)` | Game/Assets/DragLab/Scripts/Domain/GarageBusiness.cs | 89–89 | 06773E7A010152608FD6E81BFC34355AC4D39154F1531E3ED1768F333B616A6B | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| `DragLab.Domain.Mechanics.FuelState.Copy()` | Game/Assets/DragLab/Scripts/Domain/MechanicsContracts.cs | 168–168 | DBA9BE9B6573C4F3D236B814861F8FC05B18969591D533E246949173F606EBBD | 565697FE4DC81390E56AE4A518458C67D48A4D7BAD0724A3658D535FBC2A4934 |
| `DragLab.Domain.Mechanics.ComponentCondition.Copy()` | Game/Assets/DragLab/Scripts/Domain/MechanicsContracts.cs | 94–101 | DB04634FF9F1E332626CC378B837DC6D85D74C5ECE7B8349FF6768551B32253A | 565697FE4DC81390E56AE4A518458C67D48A4D7BAD0724A3658D535FBC2A4934 |
| `DragLab.Domain.Mechanics.PartInstance.Copy()` | Game/Assets/DragLab/Scripts/Domain/MechanicsContracts.cs | 124–124 | 98E77D73A4944C155EA6FF1A06F39907C61CDBEB72DC5C23C09F56FA979D0BE5 | 565697FE4DC81390E56AE4A518458C67D48A4D7BAD0724A3658D535FBC2A4934 |
| `DragLab.Domain.Mechanics.FluidState.Copy()` | Game/Assets/DragLab/Scripts/Domain/ServiceContracts.cs | 22–22 | 677AB812B94B44484DE61A3594C89C7A7B616AE850A8CF9C92ACF347E234FE9A | 221839F18A3BF8BF211C895416B5993F72BB23BBD4CEFC5AC22671D2F2329C0F |
| `DragLab.Domain.Mechanics.SnapshotEquality.Serialized<T>(string, T)` | Game/Assets/DragLab/Scripts/Domain/SnapshotEquality.cs | 19–19 | 923C403BEDFD93142848EC6053826FC399750145415E7D9FBBD08B10B75CD2C4 | A7DBAF5C217A62D950D37646B933F1B23DBCCE6CFABAE20D3A23DD8820FDBA80 |
| `DragLab.Domain.Mechanics.SnapshotEquality.Same(object, object, string)` | Game/Assets/DragLab/Scripts/Domain/SnapshotEquality.cs | 20–31 | B782BC0BABDCAAAB86113ABC4B5E325D309B28AED7FF7BEAE820E42AE34A03EE | A7DBAF5C217A62D950D37646B933F1B23DBCCE6CFABAE20D3A23DD8820FDBA80 |
| `DragLab.Domain.Mechanics.SnapshotEquality.Fields(System.Type)` | Game/Assets/DragLab/Scripts/Domain/SnapshotEquality.cs | 13–13 | 924F398059F678A2FBE1FE76561C33692AC50BAFDC0B4911E7344B18B715EBAA | A7DBAF5C217A62D950D37646B933F1B23DBCCE6CFABAE20D3A23DD8820FDBA80 |

## Field-schema source pins

| Existing source under Domain | Exact file SHA-256 |
|---|---|
| GarageBusiness.cs | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| SnapshotEquality.cs | A7DBAF5C217A62D950D37646B933F1B23DBCCE6CFABAE20D3A23DD8820FDBA80 |
| MechanicsContracts.cs | 565697FE4DC81390E56AE4A518458C67D48A4D7BAD0724A3658D535FBC2A4934 |
| PartElements.cs | 996B17FD74F45A584B5DFCBB3978699822F74CC161D46329619BC2621FE94655 |
| EngineElements.cs | F7F2BC9204CCA51C5EB96E08DC0A868786C702A7D455D7ADB7D2E73B82073EDE |
| TireDynamics.cs | BCBA0AED1D719FF2D8CD3D86561608ECCCEA4DBA0D334A5EA99A9272F5213607 |
| ServiceContracts.cs | 221839F18A3BF8BF211C895416B5993F72BB23BBD4CEFC5AC22671D2F2329C0F |
| DragSimulation.cs | 6DD7AAD73668346567EB42EAA0334217B3E1873916160AE2469CF68A817CAD25 |
| AxleDriveDynamics.cs | CA5037B3AAF228C6A15C064249D568CA49F2947985862A11D9E12C918BD9DB9B |

All existing-source bytes must remain unchanged after this prerequisite; additions are the exact new value/import/checker files plus metadata only. Preserve the current C1 Windows build as a historical baseline until a fresh build is actually produced. Complete L1D Total/Fee/Physical/Combined, caller conversion, S06 and S07–S14 remain open.
