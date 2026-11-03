# L1D pre-edit query method provenance

Captured before any L1D production edit. Source manifest: `pre-query-domain-sha256.txt` (266 files, set hash `2804DE0B7AB9F8C96D7131D13A5D566241F2E666E62AB91FBEA07D4241F6A7DE`). Complete existing-reference manifest: `pre-query-existing-reference-sha256.txt` (28 files, set hash `35C3CAB2FBDEC4092EE41FC4D92C9612CF806F51999230FC90597F4D9D5B019E`). The 24-file `pre-query-existing-legacy-sha256.txt` (`413BD83712E5A8FA7C4D54C699C189580598BA8E523F3AFD5984425F4518A46A`) remains the legacy-prefix subset; it is not the complete reference set. The file hash column is the captured manifest SHA-256 over file bytes. Method hashes use UTF-8-SIG input, CRLF/CR normalized to LF, the complete declaration plus expression/body through the balanced closing brace, and no final newline. Method-body extraction ignores braces inside C# comments and string/character literals.

## Query entry methods

| Source method | Lines | Captured file SHA-256 | Normalized declaration+body SHA-256 | Reference treatment |
|---|---:|---|---|---|
| `PartCart.Total(PlayerProfile,string,IEnumerable<PartCartLine>,ValuationPolicy,long,bool,bool)` | 46–92 | `D6A78BB5038234B0FDE0B0AD6DED505FCB628E07289DFCD2C1EF25BAB9038C05` | `5DA1B447B53182DFA20CACA02A1ADE4F77F325B86D5C6CDA4AB8F45E38DD5889` | Copied to new `LegacyCartQueryReference.TotalWithClock`; only changed dependency routes are replaced. Default clock call remains at `now == 0` position and is observable through its Editor shim. |
| `PartCart.DeliveryFee(PlayerProfile,string,IEnumerable<PartCartLine>)` | 93–97 | same PartCart file hash | `198D52F42324594396AD33BBEA6FD636C8D52847F0CCE5831557C3B3B2CAA918` | Copied. Uses frozen L1B ledger and canonical `Matches` composition. |
| `PartCart.Preview(PlayerProfile,OwnedCar,IEnumerable<PartCartLine>)` | 124–128 | same PartCart file hash | `9F7E72AF5BD26A5B65EA57CCF67F09006B12B3483269CF6284B16B3F7D959952` | Copied as `Preview`; delegates to copied Profile preview and local frozen-catalog capture. |
| `PartCart.Apply(PlayerProfile,string,IEnumerable<PartCartLine>,bool,ValuationPolicy)` | 120–123 | same PartCart file hash | `FD3094CFE501C07A0D141F3D05C45D4CA24B3B0BECDC0AFF798F2E5EFC31CA35` | Routes to independent `LegacyC1CartApplyReference.Apply`. |
| `PartCart.PreviewProfile(PlayerProfile,OwnedCar,IEnumerable<PartCartLine>)` | 129–133 | same PartCart file hash | `82BF5B0549C0140406D56ADE0EDAFE69735F2F65510D7092FAF779FC2D87C2EB` | Copied; legacy JSON clone and `MechanicsOwnership.Ensure` are restricted to the disposable reference profile. |

## Sale, reservations, catalog and owned-build dependencies

| Source method | Lines | Captured file SHA-256 | Normalized declaration+body SHA-256 | Treatment and route |
|---|---:|---|---|---|
| `Valuations.LoosePriceView` | 162–167 | `41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1` | `61A160C4DB7AEBC1CF18600D98FCD6A8C96A9BB69081D9B167376D2911185584` | Copied to `LegacyCartQueryValuationReference.LoosePriceView`, with legacy validator route. |
| `Valuations.PartValue` | 112–152 | same Valuation file hash | `480A7BF3CBA508BA2B6D371573D9CAB5FA509BE8E33CBE13B14FE3CF6C5A6D4B` | Arithmetic copied for loose part/tire/wheel sales. Tire/wheel identity helpers are source-pinned reused functions. |
| `Valuations.DisplayLooseTotal` | 168–187 | same Valuation file hash | `CA3B22C1BA9F86C83D9AF41C4881E2452BC0CD7D515999BCE6AE27FB9BD41771` | Copied as `DisplayLooseTotal`; frozen catalog Find replaces public Find. |
| `Valuations.LooseElementValue` | 188–212 | same Valuation file hash | `4F634D30F785B5A69DE3A5B6A8534AF6BADB5DB1E52B0C890C76314A628BEBFC` | Arithmetic copied for loose element/tire sales. |
| `MechanicsOwnership.ValidateForQuote` | 21–29 | `26E58FADB059138250F6E1AD801CB12564BCED39640BC7C67330EA19AE2B039B` | `6E1044726381BE2CA0DFE298CD49E278BD3E81454AD7FC05F859FE2859B939F0` | Copied to `LegacyValidateForQuote`, including exact case-insensitive `dealer` error-label test and second `LegacyValidateCore(..., true)` pass. |
| `MechanicsOwnership.ValidateCore(profile,catalog,tolerateDealerDiscriminator)` | 217–298 | same MechanicsOwnership file hash | `830E009A923C67F7E9F29186009622D60B0F7BECC6D07999793A84A8F1804BCE` | Copied to `LegacyValidateCore`; catalog Find/Spec and inventory/market/setup validation calls are redirected to copies listed below. The other direct validators remain pinned reused dependencies. |
| `MechanicsOwnership.Ensure` | 33–37 | same MechanicsOwnership file hash | `0A68BD050F1C67B859304790C44859589A8F9DA5688DD637D7B9165145A32F39` | Reused unchanged, hash-pinned normalization, only on disposable legacy profiles. |
| `MechanicsOwnership.Resolve` | 170–182 | same MechanicsOwnership file hash | `1157515D86484B8181A13D626FE52E1F213767CFA8B83DA8F4B6B33118674E61` | Replaced in new reference by copied `LegacyResolve`, which uses frozen C1 installed definitions and frozen catalog Resolve. |
| `MechanicsOwnership.Definitions(PlayerProfile,OwnedCar)` | 163–164 | same MechanicsOwnership file hash | `751137A60B21B870C31ACC2901B8C7D2B6BD8E2E9DB86C151845646514352E32` | Replaced by `LegacyC1WholePartMoveReference.CarDefinitions`. |
| `CartReservations.Active` | 20 | `17B8BF9FCA5994AB13569B42BDF38447634FA52BB127CC221AAD667DDEAF7F17` | `F833EECC7AD4E8DCC7C2EF39D36EB0E4C735594DF93C41467FE03692A88C387F` | Uses frozen L1B `.Active`. |
| `CartReservations.SameCart` | 21 | same CartReservations file hash | `01EDEBA85EEE20FFC11BEC753AACD764F4E2231AE19C8DC27FF99ECCC0642927` | Uses `LegacyCartCanonicalBoundaryReference.SameCart`. |
| `CartReservations.Matches` | 22 | same CartReservations file hash | `BE6A367D7DEA8B2AB843AD1A2C54971A77EB28F17E302CB86AEAF5686543AB93` | Re-composed in new reference as frozen `.Active && .SameCart`. |
| `CartReservations.Clear` | 46 | same CartReservations file hash | `40EEB87A1B3CD0BB7746122F328B4F87996B7585B918B48DEBBC40033F72EBE2` | Reused for mutation on disposable legacy preview only; unchanged body pinned. |
| `SharedInventory.Validate(p,identities,tolerateDealerDiscriminator)` | 109–149 | `8D85CF724547D48A6F1A9A2E58C35EE6A8B97AF087AE699C1F6DB85CE9B9E5A5` | `3F091922C4BB5E51CAB509A5839F3786D999E2FDFAF99BEC633EB91F25923010` | Copied to `LegacyValidateInventory`; catalog lookup redirected to the frozen adapter. |
| `SharedInventory.ValidUnit(p,u)` | 150–155 | same SharedInventory file hash | `4DD9FB2E2EDB55B0469E2E07F4AD84FF7411985945F7B380C305C3CBE2EA588D` | Copied to `LegacyValidUnit`; catalog lookup redirected. |
| `PartsMarket.Validate(p)` | 474–484 | `A7EDB1055D4C5B60BD3E97413023C207D199C2B05C1FBC6D2A4B353587FB235E` | `67C0C7622413967261492D01884A76677E69E7455576F4EF1354921125672E94` | Copied to `LegacyValidateMarket`; Find/Spec use frozen catalog adapter, Recipe/Fits pinned unchanged. |
| `PartsMarket.ExistingMarketIdentitySet(p)` | 485–511 | same PartsMarket file hash | `2EFBDB5945647F75ED6498A9061B4342F2225FA50E9B7A4416E7C5FC0E86EA60` | Copied along with `AddPartIds`; preserves source argument validation order. |
| `CartReservations.Validate(p)` | 54–61 | `17B8BF9FCA5994AB13569B42BDF38447634FA52BB127CC221AAD667DDEAF7F17` | `7373F50BDE5D96C49C843DB110EA2B65E75B6C3105F1721B21003957AB86727B` | Copied to `LegacyValidateReservations`; catalog lookup is frozen; `Active` routes to the frozen L1B reference. |
| `SavedSetups.Validate(setup,catalog)` | 132–146 | `21B70EC86562F023C03BFA6A590A81E074FFB92225DDC1522A267D0246BD3DC4` | `FEA377C0BED4596A457DC9B719701D1FF869530A6580DF07E66B300ABDCB36CF` | Copied to `LegacyValidateSetup`, with frozen catalog Find/Spec. |

## Frozen C1 unit-template copies and unchanged helper pins

`LegacyCartQueryReference.QueryUnit*` helpers are copied from the frozen C1 acquisition helpers; the new query file does not call current `InventoryUnits` public methods. Remaining rows are unchanged helper dependencies used by the copied code. Any future production edit to one requires an independent copy/reroute or a high review before using the oracle.

| Helper | Source line(s) | Captured full file SHA-256 | Method SHA-256 |
|---|---:|---|---|
| C1 `Models` / new `QueryModels` | `LegacyC1CartApplyReference.cs:54` | `14169EF64DF9A0F3DA550A5670FB64ED2049F5BA19D56224FD437BC5506B412F` | `F1084E7758D82572C84F756D42BDCC381AAAB97CF853270EA57F3480ABD9D068` |
| C1 `LegacyReference` / new `QueryReference` | `LegacyC1CartApplyReference.cs:50–51` | same C1 reference file hash | `3D3E54AA953EBD0D856666D91D215DB550DB857B63C937CC258B5D5278B01499` |
| C1 `UnitSpec` / new `QueryUnitSpec` | `LegacyC1CartApplyReference.cs:70–74` | same C1 reference file hash | `4713AEC963AE462FD82CC7E1E687559E5AACEED4DC288FB7F4BC1A13A5335C0E` |
| C1 `UnitTemplate` / new `QueryUnitTemplate` | `LegacyC1CartApplyReference.cs:75–79` | same C1 reference file hash | `79116F759CA288E76837FEBD9080945DD29E4FF7637E636257F672C3D514FE53` |
| C1 `UnitCount` / new `QueryUnitCount` | `LegacyC1CartApplyReference.cs:80–81` | same C1 reference file hash | `EC5B9E49E8C6CDFA978376A9C1431141A4308247A3EE11E5A8A096AB73CEEB46` |
| C1 `UnitRoles` / new `QueryUnitRoles` | `LegacyC1CartApplyReference.cs:82–83` | same C1 reference file hash | `476E14108084C5FABCC84B064A4C3EF3DE67A8E1FD89F164800B9BBAB8989A3C` |
| C1 `UnitPositions` / new `QueryUnitPositions` | `LegacyC1CartApplyReference.cs:105–110` | same C1 reference file hash | `7D6E3980B00FA2DA9018E72BA9508852040162B318CE5D100CE4261F805C927B` |
| C1 `LegacyTireEnsure` / `LegacyTireSyncSummary` | `LegacyC1CartApplyReference.cs:171–179` | same C1 reference file hash | `F1B2C473D2BAD0F04C53104226ADD4DAF745C0E5E2B9EF8BF5B37C7C9A52F98F` / `99E532F3D5B833B5BB475A9D99A833186837A98055951AD213F13F00720B4DFF` |
| C1 `LegacyCreate` / `LegacyEnsureNewPart` | `LegacyC1CartApplyReference.cs:182–197` | same C1 reference file hash | `65D48109667B09E106A030EB60F84E5BA9196A1EA01859C2F181F0FAE2851BD8` / `C77BB92F11510239A4C568967FD8DF78C37FDCC284FC9BD814009C1E6A470807` |
| C1 `LegacyPerValve` / `LegacyRole` | `LegacyC1CartApplyReference.cs:198–199` | same C1 reference file hash | `EEFA5F9B67E6012FE6CD38EA2BFAB4985B4CD12AEF8074188AEAFD7FCAAD2DBC` / `D0522459979E720FFEE4B760744EE599F39707265E39FFA0386CB455F1AC9EDF` |
| `InventoryPricing.NewSet/NewUnit/BuyUsed` | 159/160/161 | `EAED6C71F9AADBCB969E5B0A20A7A6FE3D6E54D4771D4711FC008F318BE428FD` | `92EE4D625D5B9B4DA700F0A2C778595DF0C5E71B050E95939E16CD1B766051D6` / `42B9B290EA7B5E76B1C240FBA1F7DD27ADFCBD9E6A3FC391202434582431F0BB` / `DC03F0A429BDAA6DDE5D7F1548EB94D6660E78D2B011421FE0A8D261895F80DC` |
| `EngineElements.Count/ValveCount/Ignition/MatchesCategory` | `EngineElements.cs:65–68, 111–114` | `F7F2BC9204CCA51C5EB96E08DC0A868786C702A7D455D7ADB7D2E73B82073EDE` | `82E04B65168098BF46CFD29AB5895ECEB303DB82E5AFA8F9997B4F51105E2306` / `DA7B1AB1D9BC63D63AC232C31F2F9F6E618DE37994E50EE4BED87AEA92997E76` / `5D7040C5E4DB847E2522753D0A351D4F5936247266E954EFED1CF5D2CFCFB742` / `5F93317483A065291574FEAF5E7A0B45188314B7542E5CB29E151C236E3B03FB` |
| `PartSemantics.Friendly` | `PartSemantics.cs:18` | `6A67B8B4BCFEFC3964052A56652642B8EA179A5DEEFB3241121526DDA787AB3A` | `474A8B5E28621E95962B3B8BA0F61FC24AB99D9A931434A74CDE64DBE1F0107E` |
| `EngineRebuilds.RequireAvailable(OwnedCar)` | 87 | `698D0BACD59E7F03FF9027266CFB29DDB075D91F68C62A195E8DF6B5CB21BE6D` | `5DD7D5FA04B3273B1A415BFA2B1BAC19BF1220CB756838219435E15E502F00C6` |
| `MassDynamics.ValidateInputs` | `MassDynamics.cs:24` | `09FDCA32048859EE507D726137E5B52468D1382BF043A6FD7E9AB388986FDA5B` | `9977D61AAA2E9ED5DEFE886B19286609239E616BC874ED53E27944AED3B5724E` |
| `MechanicsSchema.Finite(double)` | `MechanicsContracts.cs:13` | `565697FE4DC81390E56AE4A518458C67D48A4D7BAD0724A3658D535FBC2A4934` | `70654AEDCB8064E3CBC264049A94A01FD31841FCC65D7E6C3A65210BFBB34033` |
| `MechanicalCatalog.EngineSlot` | `MechanicalCatalog.cs:176` | `7B99E7ECE021FF5DD811FA76981963A69036251275271D743FEB26449711C932` | `5F5A697F434564100791CEC9DB6F69BF1099BFE141993FD0FAB528BF258F0B26` |
| `MechanicalCatalog.DisablingEngineFailure` | `MechanicalCatalog.cs:177` | same MechanicalCatalog file hash | `DBF7901B99C745FA8D7F61F54CF1A50E23FBB540A001ADCA2AE7694AD0689519` |
| `EngineElements.FaultAllowed(PartCategory,EngineElement)` | `EngineElements.cs:145` | `F7F2BC9204CCA51C5EB96E08DC0A868786C702A7D455D7ADB7D2E73B82073EDE` | `91EE8EAC6F0B9796F1A48F78DBF6C0C69B33A0FAA7E4205D28CE478FE2E30FF4` |
| `AssemblyOperations.Parse` | `AssemblyOperations.cs:55` | `70DB37E1A5295F5F33C39A119F9E8F1AFD94B8EF67B4A1FAB3475950F692253E` | `63544906CAB3FE040C157E3CA9D062950462368F3B387D6AA605FC4A9FC86402` |
| `PartSemantics.Wears` | `PartSemantics.cs:23` | `6A67B8B4BCFEFC3964052A56652642B8EA179A5DEEFB3241121526DDA787AB3A` | `96672FED72D34BC6A549BD8CB5060E18B7E56497F6BAFA349FB2E7BDBF3BFE30` |
| `VehicleListings.Validate` | `VehicleListings.cs:89–96` | `FAB879227D1003690CC83193CA5E4E69C66B1247CF67634299162597C6AC7250` | `6B0BBB4BB7598B10222B4C773BC7437C7D35045CFF2872580DEB72393ACB9FD3` | Not called by the copied quote validator; this public wrapper owns/cache-publishes state. |
| `VehicleListings.ValidateCore` | `VehicleListings.cs:97–99` | same VehicleListings file hash | `CF0E591D61963E5993D3BBB1DC1FAF59AC37E815941898E614EECA6D5CA6888A` | Current core copied as `LegacyValidateVehicleListings`, with catalog reads redirected to the frozen adapter. |
| `VehicleInspection.Validate` | `VehicleInspection.cs:44–49` | `5D27ABC7ADA04228150F61FFF2A6AE6F7F2DFAD7647BBE0D65279E29E400D700` | `F752F0A5ACDAA8A0A2BE61AF456B134ACF4CA3179FD8348166678E3DAC50B977` |
| `PaidEconomy.ValidateState` | `PaidEconomy.cs:65–72` | `38F4AA9C534BA76BD0B9DDD1B4F4D951FBA8BA95F447A92786815C1F6D172045` | `A1FCA82D4CEA3221AC70A28A6BB59CBAA8FE0539384D63F6E6F047F866B443FD` |
| `CareerProgression.Validate` | `CareerProgression.cs:67–71` | `A7A16F60ED5F919D8D0859B85FEA18823868111978844C05F22A095CC6190E74` | `75BA6C086CBA6F02F4DDA51944B3F47E086D77E87AD0D69CB1E366C1FF4F6DA7` |
| `GarageBusiness.Validate/Reserved` | `GarageBusiness.cs:104–109` / `110` | `DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732` | `D6AD538B65DAA91210220CCB03F52D48EE3144090D6E818A189EB217C590DA2B` / `5F5DD538520B63DFFD66515224C6EAC3B4F5D8687436C1BF04C8071A40F2AFE5` |
| `OwnershipValidationLookup.Capture` | `OwnershipValidationLookup.cs:38–42` | `32B7EF1FB4FE7671E8A66D4B7852115CF698CA32C16CEF81FC57C67FD7FBD176` | `2C3590CF40ABE9E08B40D00293CC1EE0B0CBFDBFD0398C31C1576A593A89C3E9` |
| `OwnershipValidationLookup.FindEngine/FindPart/FindCar/CarContainingPart` | lines 107–113 / 125–131 / 89–95 / 155–162 | same OwnershipValidationLookup file hash | `129DE8302965EA7DE265B8AFBC5EB515FD0EB241EBD89E0317200EC767229435` / `13B4C853DF2CFB520EE1CC07A33C086F13AD6EA718EA2272484FEBEC51371A32` / `C2E630FC98AF62083314C91144C2203E9B649E9C6C5EB775DA91307F89EA0B38` / `7D6BF159E14864026B9D2E9B32FD5988E00A576B89F9918EBFDED438307045A6` |
| `EngineRebuilds.Validate` | `EngineElementService.cs:106–107` | `698D0BACD59E7F03FF9027266CFB29DDB075D91F68C62A195E8DF6B5CB21BE6D` | `DCC4DEA5ED2E1EED1E59396A61FB770D89147E9320FE1FCF3E88E477E77E7CF1` |
| `MechanicalCatalog.Spec` | 108 | `7B99E7ECE021FF5DD811FA76981963A69036251275271D743FEB26449711C932` | `7602678DB5EF358D7A7AE4396E18218348F7490B0A625903790F049FC82C87A6` |
| `MechanicalCatalog.Find` | 109–115 | same MechanicalCatalog file hash | `1316E3E549C98FEB639F625EE20C0C9080FD6EA5E4EB1674B4FA39905FC344A2` — query routes instead to frozen `LegacyMechanicalCatalogReference.Find`. |
| `MechanicalCatalog.Recipe` | 265–285 | same MechanicalCatalog file hash | `1403E012A60499A8F8C793FA0FBBA5EDBE236C06D5E194253B15B50DB230DA1C` |
| `MechanicalCatalog.Fits` | 292 | same MechanicalCatalog file hash | `23FB2B64E3994E8BF7E57512B124262889F0C697E51EFC0D4B17F083FCE87567` |
| `MechanicalCatalog.Resolve` | 305–306 | same MechanicalCatalog file hash | `A05D2F8404F2F4B931E071F9E7E0C6BDF98979E1AF1CBA28DC9A0DA4577166FC` — query routes instead to frozen `LegacyMechanicalCatalogReference.Resolve`. |
| `MechanicalCatalog.FitsDetached` | 294–304 | same MechanicalCatalog file hash | `A5D228CFAD11CE2154353E0A3D92204F3E33B1677B16C6ED221BEB2E006011F1` |
| `ValuationPolicy.Defaults` | `Valuation.cs:36–50` | `41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1` | `FCC872EA8F1D71F6F12F8A87B4527A94C3BC02A49067FB2AD5744D18768E9259` |
| `RuntimeEntropy.UtcNow` | `RuntimeEntropy.cs:23` | `2A36D543B68432B056DD034834C7B6BED595D055C279DE2F0FF6CBE3C8BD0FA4` | `40040FD26BA6744BACD6887C41C603E7FCADC560D0E249CCB40A75A7D8B735DD` — only reached by no-shim reference Total when `now==0`. |
| `PartsMarket.Calculate` | `PartsMarket.cs:447–451` | `A7EDB1055D4C5B60BD3E97413023C207D199C2B05C1FBC6D2A4B353587FB235E` | `279D8F696C140B20C9EF8E899905C6679662DFE2DB9EA5A37DF3F34B7F8456E8` — reached inside the frozen L1B supply reference. |
| `MechanicsOwnership.NewId` | `MechanicsOwnership.cs:17` | `26E58FADB059138250F6E1AD801CB12564BCED39640BC7C67330EA19AE2B039B` | `661C259A9D07D4BEB06121749A216C2E786B05B29F306456CF498D892D549B99` — only C1 Apply calls on disposable reference profiles. |
| `WheelFitments.HasOverrides` | `WheelFitments.cs:10` | `97EBCFA4184C106D0E50440AB9864070B40743F8911D80E2B83941B9B68C5B2A` | `FE6A9409DDBE2D201CF127D3432B8B37C689BE882E564345269E28AC1E9F56A6` |
| `WheelFitments.DefinitionId` | `WheelFitments.cs:9` | same WheelFitments file hash | `4CC8A6105A24306E7C8F638A4B46ECEB1AD248A705C0FD5E61D3063B32FF5CC0` |
| `TireFitments.DefinitionId` | `TireFitments.cs:10` | `E0647684D76D549ED88DCC29083865360426FC14454AE42CC7F0615C83EAB89C` | `49F8E1DD88A039AF0A945B6BD1805464489BEDF446ECFA3C5172990AF9FEF933` |
| `TireFitments.Capture` | `TireFitments.cs:31–42` | same TireFitments file hash | `DD6A84CB7DF47DDDFFAAAD64B0A56E64B2D8419686B182FBA3382489A7BBD8F5` |
| `WheelFitments.Capture` | `WheelFitments.cs:16–22` | `97EBCFA4184C106D0E50440AB9864070B40743F8911D80E2B83941B9B68C5B2A` | `95AF8246BA91C90097CF07A4B48DD624AD438F0448D6CABF09A4CCDD641EFEE4` |
| `InstalledBuildResolution.Resolve` | `InstalledBuildResolution.cs:19–47` | `622491EB872AA92B42F1BECCDC82A9044B8F9CD18F2A082D8DC4B88A21D86750` | Not called from new copied `LegacyResolve`; catalog resolution routes through the frozen catalog adapter. |

## Direct model-validation leaves reached by the copied quote validator

These are invoked from copied `ValidateCore` / copied leaf-validator bodies. The entire source file is pinned as well as the normalized method body so inherited overrides and same-file shared contracts stay reviewable. The query copies do not route through the candidate quote validator.

| Current helper method | Lines | Captured file SHA-256 | Normalized declaration+body SHA-256 |
|---|---:|---|---|
| `ValuationPolicy.Validate` | `Valuation.cs:51–62` | `41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1` | `AD1A58F9DEF960390A5B73C9EE6E4E9B9F28AD74574E078AFCCD2E72FCE39D46` |
| `ClutchConditionState.Validate` | `MechanicsContracts.cs:25–32` | `565697FE4DC81390E56AE4A518458C67D48A4D7BAD0724A3658D535FBC2A4934` | `74BD6B4280482B956ED0C660FE0FF0C3E447590BD09F7D3D68880558460926E1` |
| `ComponentCondition.Validate` | `MechanicsContracts.cs:102–113` | same MechanicsContracts file hash | `DB827D57EECD0DA727FCB94730911089BA8DE1A6FBEE18A20C96DEDDE6662AD0` |
| `PartInstance.Validate` | `MechanicsContracts.cs:125–135` | same MechanicsContracts file hash | `C2F5AB267B3FD9FACA6B4E41A0512CC933E84BF6B935749CA9DF4262C1F7B52B` |
| `FuelSpec.Validate` / `FuelState.Validate(FuelSpec)` | `MechanicsContracts.cs:156–162` / `170–175` | same MechanicsContracts file hash | `6498163EAF8C566353BD606474FD5AB51414B626840DE1F14EEB1A9602190623` / `FD1DEA69B950448B666FC75DF7E25374867C408CA587B2E08A312C3B6650B808` |
| `FluidState.Validate(FluidSpec)` | `ServiceContracts.cs:24` | `221839F18A3BF8BF211C895416B5993F72BB23BBD4CEFC5AC22671D2F2329C0F` | `1D3F7FAEE79DE40742D47FDC1E0162A41ED243D02CD2D96416396F82FCB557E6` |
| `EngineElement.Validate` | `EngineElements.cs:15` | `F7F2BC9204CCA51C5EB96E08DC0A868786C702A7D455D7ADB7D2E73B82073EDE` | `A5FBDD98528EAA7E3A0E27D436868C4B2C339E601A6DB073715A6034DA0C2BBD` |
| `CylinderMemberElement.Validate` / `ValveTrainMemberElement.Validate` / `WheelElement.Validate` | `PartElements.cs:22–25` / `33–36` / `53–57` | `996B17FD74F45A584B5DFCBB3978699822F74CC161D46329619BC2621FE94655` | `59D051D1A8FE27556653C411CF6046F461778C84E8BAA1C48B58EDC013AEF401` / `8823C249CE8AD6EDFCE336ABC84F6158DE2C9DBF109BFE5DF21A2793DF13BBC1` / `5F12196DB97B56E81054792BD5121C87FBB133FD44047214B33B5334CCB6AE4E` |
| `TireCondition.Validate` | `TireDynamics.cs:10` | `8F58DE5312B224EF7136EEB2EDC949BAEB3FAB20685A52D2E0530A58B4613E4B` | `79663416CA35103CBEEC8B70DACEA465512A1F168222DB4E58BA317C68B6B291` |
| `AwdSpec.Validate` | `AxleDriveDynamics.cs:18–27` | `BA32F161FD19AC7D86908CAF85592FA5EF9C12DE33C6836EDA051E2E79B70922` | `8762CEF94A8FEB9A87E4BE69121BEE05D5ADE9F1BD28C0EC1142258A04D2D604` |

## Copied valuation leaves and remaining direct predicates

The valuation arithmetic leaves below are copied into `LegacyCartQueryValuationReference` under the corresponding `Legacy`-free local names. Source and reference ranges are independently hashed; the helper rename and formatting changes mean body hashes are recorded per side rather than asserted equal. `Factor` and `Unique` remain direct unchanged calls made by the pinned `ValuationPolicy.Validate` method.

The source file pin for all `Valuations.*` methods in this table is `Valuation.cs` SHA-256 `41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1`. The current reference file pin after the syntax-equivalent whitespace cleanup is `LegacyCartQueryValuationReference.cs` SHA-256 `7610C015AA6812F88BF1C6C44679EE0239B7B6C9D058C2F058E9F06CA9F078E7`. Reference B executed the pre-cleanup file `18A72CB0EF1F32B52F26D749562CA8B5A0B6C78DB9F56F98FEA833D940FD5F3C`; both identities are retained below.

| Source method | Source range and method SHA-256 | Reference method | Reference range and method SHA-256 |
|---|---|---|---|
| `Valuations.Clamp` | `Valuation.cs:96`, `F48D02AED61495045DD408D8D104D6E5B1FC72EEA3E19775D21228F023F665A2` | `LegacyCartQueryValuationReference.Clamp` | `LegacyCartQueryValuationReference.cs:16`, `34D378B34B71BFC75E052B7860B43A632B7885ADFF2E4A15A8F47BF199DCD536` |
| `Valuations.Scale` | `Valuation.cs:106`, `C45F5F39F98F19DD1624AE9A6E7F996324F284935E0C73CE2A18A1013DA15E16` | `LegacyCartQueryValuationReference.Scale` | `LegacyCartQueryValuationReference.cs:17`, `C45F5F39F98F19DD1624AE9A6E7F996324F284935E0C73CE2A18A1013DA15E16` |
| `Valuations.ElementHealth` | `Valuation.cs:98`, `80D36AF5017F77288494BAAEF9DC606FC2DBDD7E6FFF23C642BEDBA42BCA3771` | `LegacyCartQueryValuationReference.ElementHealth` | `LegacyCartQueryValuationReference.cs:18`, `141FEFC49D060F4D3C9225EB0B488FE25DB4B26F21E8234D25FBAB56B7495681` |
| `Valuations.Health` | `Valuation.cs:99–105`, `0588C65AA4D8000EAFC6DDA2490AD1BBC8BDD40083468B692174FE25D0BD8363` | `LegacyCartQueryValuationReference.Health` | `LegacyCartQueryValuationReference.cs:19–25`, `B78F1A35355C8B7879F8344EB7F573F585D2D53FB49950F57D386A63922A9614` |
| `Valuations.Fault` | `Valuation.cs:97`, `0056DF8DA75E586AF8E1BBD26117C28E2696A862656298C02A0932D8B973C918` | `LegacyCartQueryValuationReference.Fault` | `LegacyCartQueryValuationReference.cs:26`, `28B56489F3FF960F3B54B039A6116AB7BAF8A79292C2773A2B0456DA5B0D542C` |
| `Valuations.Buyer` | `Valuation.cs:108`, `B5B1E2990E699D50CC7DE069F9F7F5F4D7B2B16EB50243EF5C0672B30AFCEEB2` | `LegacyCartQueryValuationReference.Buyer` | `LegacyCartQueryValuationReference.cs:27`, `D2C54C8EF9CA223EF7D225F7105381CF0DD061BC7DE990542F6611877B397E19` |
| `Valuations.Quality` | `Valuation.cs:109`, `1C723E77675CE29CF94187B5138A0864DDB81882911068FCA454661B7FBA4CCA` | `LegacyCartQueryValuationReference.Quality` | `LegacyCartQueryValuationReference.cs:28`, `699DC865F94837AEA005FEDA3A782C59EEEEC0A0A6829B2FE63D6CDCE9EF22E4` |
| `Valuations.LoosePart` | `Valuation.cs:110`, `FE64C9EB30638A18BFCA8368E89ED207C2F5A74F4E8CC2CC587F7EAD85D9432D` | `LegacyCartQueryValuationReference.LoosePart` | `LegacyCartQueryValuationReference.cs:29`, `541CB03BF024F4D13B9FEF42B7B62FBA11F794E26B43503CE4A6D99D84911EC8` |
| `Valuations.LooseElement` | `Valuation.cs:111`, `2587395174A280595E87237FD6407D8621866859D8A2902E818564605E81C17A` | `LegacyCartQueryValuationReference.LooseElement` | `LegacyCartQueryValuationReference.cs:30`, `28BA535787316584B856FFE33E50A2F56B8FA193365AC61A3AC146179F13398F` |

| Reused direct method | Source range | Captured file SHA-256 | Normalized declaration+body SHA-256 | Reached from |
|---|---:|---|---|---|
| `ValuationPolicy.Factor` | `Valuation.cs:63` | `41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1` | `2DD1A98991D67E4F77B41B8F0504485F5C74C2C854872AD7653A7D6A6C565C46` | `ValuationPolicy.Validate` |
| `ValuationPolicy.Unique` | `Valuation.cs:64` | same Valuation file hash | `0F88A2FE1B4B951CA99C0F3CEED35331E64D1293D1FCD955513A9737D35CF067` | `ValuationPolicy.Validate` |
| `EngineElements.Supported(PartCategory)` | `EngineElements.cs:66` | `F7F2BC9204CCA51C5EB96E08DC0A868786C702A7D455D7ADB7D2E73B82073EDE` | `7FF6169F2C52E8AA6C26EF7422A5527CA63AFB4E6077E239590478CA2F3FF42E` | Copied `LegacyValidUnit` |
| `SavedSetups.InScope(SetupScope,PartCategory)` | `SavedSetups.cs:44–45` | `21B70EC86562F023C03BFA6A590A81E074FFB92225DDC1522A267D0246BD3DC4` | `D07F6B1B7D31F66873CE47CA9DEA8212E0D2DB0B027B6D0AB50E11D135B70175` | Copied `LegacyValidateSetup` |
| `SharedInventory.StorageOf(string)` | `SharedInventory.cs:50` | `8D85CF724547D48A6F1A9A2E58C35EE6A8B97AF087AE699C1F6DB85CE9B9E5A5` | `EFAAFEB36F63201040C234F8DB08BA38AE12E56E732A31A02BB37B6ADE452B04` | Copied `LegacyValidateInventory` |

## Frozen reference file pins and routing

The 28-file complete existing-reference manifest pins all pre-existing reference artifacts, including non-Legacy-named files; the 24-file legacy-prefix manifest is only a subset. Query-reached references include:

| Reference file | Captured SHA-256 | Used methods |
|---|---|---|
| `LegacyC1CartApplyReference.cs` | `14169EF64DF9A0F3DA550A5670FB64ED2049F5BA19D56224FD437BC5506B412F` | `Apply` |
| `LegacyC1WholeEngineMoveReference.cs` | `F94E73C0C6FAC0C1F16AB0AAB810EE29638D8949FD3A70549BCE840AF8529D76` | `Move` |
| `LegacyC1WholePartMoveReference.cs` | `6F18E40E6075E2C3B6BA5BDD5ABE30CE0F86C86151686CE103FCBD7302EF491F` | `CarDefinitions` |
| `LegacyScopedSupplyReference.cs` | `AE960159340F24C10ED32136B6C6F125154A33355AAF5496D2FAFED5DBD09DCE` | `Active`, `Held`, `Ledger` |
| `LegacyCartCanonicalBoundaryReference.cs` | `0CC015A149A062BBCC76281D5DAD9CFC8A3736B5D5BC5D2288EFC24530DE44EE` | `SameCart`, `ProductCacheMiss` via frozen L1B ledger |
| `LegacyMechanicalCatalogReference.cs` | `2C82E8503226148C8F5C310A62DEA61495A329C396825E798630CD45B0370112` | `Find`, `Spec`, `Resolve` |
| `LegacyCartPreviewOracle.cs` (body source only; not called) | `E691597B6EE61220436401EBCA90B04B19D4AFFB73FBB096E5575359816391F3` | Literal source for copied Total/Fee/Apply/PreviewProfile ordering. |

No L1D candidate method is called by the new query reference. Reused production helpers are explicit method-hash dependencies, and copied validation/catalog paths route through the frozen reference implementation where they have a changed C1 boundary. This preparation is not a claim that the functional L1D query checkpoint is complete.


## Post-liveness whitespace-only cleanup

Staged whitespace validation found trailing spaces on two blank lines and extra EOF blank lines in new reference artifacts. Root cleaned only that trivia after terminal Reference B and after full post-run source/process checks. With `UNITY_EDITOR` defined, Roslyn `SyntaxFactory.AreEquivalent` is true for both complete reference syntax trees, and every token kind/text matches exactly (query 4,752 tokens; valuation 7,030). GUIDs are unchanged. This is syntax-equivalence evidence plus final compile E, not a second full liveness run.

| Artifact | Reference-B file SHA | Final file SHA |
|---|---|---|
| `LegacyCartQueryReference.cs` | `24E79E520484EB90E20099F14E609E4E6EC01A3CB8FC53B49BA31E001FC2BA15` | `6686A8D884A41B8B01CE2666546E56DAF3A59D28534B3FBD104740A817BBB754` |
| Its `.meta` | `AA39D03DA2E12B71E467B14202AA7C393D609CF7122E475B3F7EA4667B6D11C5` | `E2D2D0F0BD2435410A9E8F59193CC10D99F6B3E5E0548811F02F017A8FDE772F` |
| `LegacyCartQueryValuationReference.cs` | `18A72CB0EF1F32B52F26D749562CA8B5A0B6C78DB9F56F98FEA833D940FD5F3C` | `7610C015AA6812F88BF1C6C44679EE0239B7B6C9D058C2F058E9F06CA9F078E7` |
| Its `.meta` | `7D16DDDEF71E4D8E25A80656F0C1AA9FE956BEFCEFBCD545ED919E791312D44B` | `2E6264D31E455D992BF61A39CE6D45AE1390F8785FDAF4F957EC1EFE454BAFAD` |

All query declaration/body hashes and 22 of the valuation file's 23 method hashes are unchanged. The only method-byte hash delta is a trailing-space-only blank line in `LegacyExistingMarketIdentitySet(PlayerProfile p)`, lines 275–301: old `638869DADEE2E061D5982A1A0E59CF9E402BC2174FA825E06DEE6F0CF4A26B2A`, final `E09F1785F79974D18BE180405315C8CDFE0583CACAA5AC742A46B67E3D6FDCC7`. Its tokens and complete syntax remain equivalent. Original production-source pins, all nine copied valuation-leaf method hashes and all 28 existing-reference files remain unchanged. Final byte hashes still need independent review before production authorization.
