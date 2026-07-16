# Changelog

## [1.18.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.17.2...v1.18.0) (2026-07-16)


### Features

* **invoices:** improve PDF documents with loyalty details ([d3d4ce5](https://github.com/jab16jy/Sistema_Pos/commit/d3d4ce544feca5f3ade2a8acfdaacea04758efa1))


### Bug Fixes

* **ci:** use explicit es-CO culture in test and tolerate artifact upload quota ([8dd83ef](https://github.com/jab16jy/Sistema_Pos/commit/8dd83efa785b6757178c2b2f6f83486a73f8b16a))

## [1.17.2](https://github.com/jab16jy/Sistema_Pos/compare/v1.17.1...v1.17.2) (2026-07-16)


### Bug Fixes

* **inventory:** restore movement history reload after selection/adjustment resets and fix UTC display ([4e05aff](https://github.com/jab16jy/Sistema_Pos/commit/4e05affca4766ec68820b763ca991a2b18a476cf))

## [1.17.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.17.0...v1.17.1) (2026-07-16)


### Bug Fixes

* **auth,cashier:** enforce username uniqueness constraint and update cashier workflow ([e52df06](https://github.com/jab16jy/Sistema_Pos/commit/e52df069e7a895bf0702c33858eb53fc8dfe399f))

## [1.17.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.16.1...v1.17.0) (2026-07-16)


### Features

* **cashier:** add autocomplete button for saldo diferido ([a9d179b](https://github.com/jab16jy/Sistema_Pos/commit/a9d179b4ac07212acd3d72646ae5d72273b0f045))
* **customers:** resolve concurrency bugs, UI issues and language inconsistencies in customer module ([8bfdfb9](https://github.com/jab16jy/Sistema_Pos/commit/8bfdfb9821f0f627a61d25b804d1b29a7db9d951))
* **settings:** add configurable maximum deferred balance percent ([8009ee2](https://github.com/jab16jy/Sistema_Pos/commit/8009ee2e08b2cd81209dcf505917ced6e6d3323e))


### Bug Fixes

* **audit,shifts:** resolve design and logical bugs in turnos and auditoria modules, and other modifications ([0a5f1e7](https://github.com/jab16jy/Sistema_Pos/commit/0a5f1e7a76d77f8efb6388b93f743467316f620e))
* **customers:** pagar todo el saldo button never enabled ([f3d9a27](https://github.com/jab16jy/Sistema_Pos/commit/f3d9a27198e3ee6cca4603233db0ce8cec80dc2b))
* **loyalty:** don't award points on a deferred balance until it's paid ([8cd55e5](https://github.com/jab16jy/Sistema_Pos/commit/8cd55e5437ddf96be759eadfc81dd2e220d5111b))

## [1.16.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.16.0...v1.16.1) (2026-07-15)


### Bug Fixes

* saldo, abonos y consistencia cartera-caja turnos-facturas y anulaciones ([5c3a2f8](https://github.com/jab16jy/Sistema_Pos/commit/5c3a2f87992aaef4b61d6dc871f2698b5662a90d))

## [1.16.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.15.0...v1.16.0) (2026-07-15)


### Features

* **cashier:** improve payment validation and reporting ([a61f1c2](https://github.com/jab16jy/Sistema_Pos/commit/a61f1c24cd939f8f4c8e3b14b675f1f49b1a3f77))

## [1.15.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.14.2...v1.15.0) (2026-07-15)


### Features

* **cashier:** add cancel held sale with stock restoration ([e560e57](https://github.com/jab16jy/Sistema_Pos/commit/e560e57ba6c888fb826b6a9a0ae1d5fd7068edbd))
* **cashier:** complete hold/resume, admin discounts, cancel/shortcuts, fix loyalty race ([f2cc2f3](https://github.com/jab16jy/Sistema_Pos/commit/f2cc2f3f60e7ac0d3d41c1d8a0a94a70f7d11406))

## [1.14.2](https://github.com/jab16jy/Sistema_Pos/compare/v1.14.1...v1.14.2) (2026-07-15)


### Bug Fixes

* **inventory:** correct low-stock rule and allow backdated shift openings ([b246f22](https://github.com/jab16jy/Sistema_Pos/commit/b246f220e49180d85c2096cc7198832873d7455e))
* **purchases:** manual invoice number, supplier creation flow, and consistent preview UI ([1da81c8](https://github.com/jab16jy/Sistema_Pos/commit/1da81c86b691238a0677260cd2d7964be68a80f6))

## [1.14.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.14.0...v1.14.1) (2026-07-13)


### Miscellaneous Chores

* **release:** prepare v1.14.1 ([c419608](https://github.com/jab16jy/Sistema_Pos/commit/c4196085e902c2a59038673e2c78e39ab0156de8))

## [1.14.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.13.2...v1.14.0) (2026-07-13)


### Features

* **printing:** add business logo, longer paper spacing, and loyalty footer to thermal receipt ([308725d](https://github.com/jab16jy/Sistema_Pos/commit/308725df2544bd24bff067ef512f8dffecf738a8))

## [1.13.2](https://github.com/jab16jy/Sistema_Pos/compare/v1.13.1...v1.13.2) (2026-07-13)


### Bug Fixes

* **printing:** add customer phone and email to thermal receipt ([31ebbb9](https://github.com/jab16jy/Sistema_Pos/commit/31ebbb93e68324577a8c69c84de3d51096bf7563))

## [1.13.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.13.0...v1.13.1) (2026-07-13)


### Bug Fixes

* **printing:** show customer and loyalty discount on thermal receipt ([b4da8dc](https://github.com/jab16jy/Sistema_Pos/commit/b4da8dc0b9591f466903e2f0a9f9b084fc0bd9a3))

## [1.13.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.12.0...v1.13.0) (2026-07-13)


### Features

* **settings:** add explicit printer port override ([c2dbff1](https://github.com/jab16jy/Sistema_Pos/commit/c2dbff1bbd4f6d546e63e27c68afa30686d55423))


### Bug Fixes

* **printing:** send Latin1 + WPC1252 codepage instead of UTF-8 ([d64463c](https://github.com/jab16jy/Sistema_Pos/commit/d64463c2f9ebc52429f8587a00cc492d4b934232))
* **products:** rename stock field label to "Stock inicial" ([3a60c71](https://github.com/jab16jy/Sistema_Pos/commit/3a60c71d9a47226e48c6b0ea8cbca25770670b06))
* **products:** stock is never editable from Productos, even on create ([6a5857e](https://github.com/jab16jy/Sistema_Pos/commit/6a5857e982feb44da7eb62e583d41112097dc767))
* **users:** show "Cajero"/"Administrador" in Users/Roles UI only ([bf92922](https://github.com/jab16jy/Sistema_Pos/commit/bf92922ad798800eef669d37a7eb1f723978539a))

## [1.12.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.11.0...v1.12.0) (2026-07-13)


### Features

* **products:** accept CSV/XLSX catalog imports and add a file picker ([b475cb8](https://github.com/jab16jy/Sistema_Pos/commit/b475cb8e44d7ef12f2bee9763b867772a284b531))

## [1.11.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.10.0...v1.11.0) (2026-07-13)


### Features

* **loyalty:** fix point accounting, add compensation ledger and tier text foreground ([3480937](https://github.com/jab16jy/Sistema_Pos/commit/3480937304e545b0028cd2a204ba95e162c19609))

## [1.10.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.9.0...v1.10.0) (2026-07-13)


### Features

* **loyalty:** add color picker dialog, accept dot/comma in tier multiplier, and improve cashier search behavior ([2d7f826](https://github.com/jab16jy/Sistema_Pos/commit/2d7f826a4c6d50149dacb854e08eaa5b05ac2bed))

## [1.9.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.8.0...v1.9.0) (2026-07-13)


### Features

* **invoices:** improve validation alerts and add discounts/loyalty points summary in invoice detail ([3591891](https://github.com/jab16jy/Sistema_Pos/commit/35918919f82cac05f19a5c7133699141c4dc56fd))
* **invoices:** show loyalty discount and actual amount paid in PDF ([74e5518](https://github.com/jab16jy/Sistema_Pos/commit/74e5518c8d4b9bbfb2df7fe8aa9f9e7e9121e207))
* **loyalty:** add configurable tier colors and wire discount/multiplier benefits ([2595e13](https://github.com/jab16jy/Sistema_Pos/commit/2595e130eb69341ba7c799643f18fac2b3eda1f0))
* **reports:** implement PDF and Excel exports for cash closings and audits tab ([370887f](https://github.com/jab16jy/Sistema_Pos/commit/370887f85ede4e07616d850446e2d027e4ff1579))
* **reports:** include margin analysis in top sellers export and optimize QuoteView layout ([cc64ea6](https://github.com/jab16jy/Sistema_Pos/commit/cc64ea6ee8ed48763103032b4e3c771106e204bc))
* **shifts:** persist turno closing snapshot and reconcile counted cash against expected ([4f65d13](https://github.com/jab16jy/Sistema_Pos/commit/4f65d13618729ceeb45098e7b20898a54215b5df))


### Bug Fixes

* **settings:** gate SMTP/WhatsApp config behind Settings permission, not admin-tab nav ([856f0a3](https://github.com/jab16jy/Sistema_Pos/commit/856f0a3fb38b0bbb326954a1584c3fdcbcc70b0b))

## [1.8.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.7.0...v1.8.0) (2026-07-13)


### Features

* **admin:** add human-readable permission labels and redesign roles/users screens ([ba41db3](https://github.com/jab16jy/Sistema_Pos/commit/ba41db3f6825bb382dabf5cc01483da8dcda0a3d))


### Bug Fixes

* **ui:** remove redundant Configuración tile from Admin hub ([6a0d1d0](https://github.com/jab16jy/Sistema_Pos/commit/6a0d1d08bbc0a47643c2c48efa5c477ccfdaefd8))

## [1.7.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.6.0...v1.7.0) (2026-07-13)


### Features

* **auth:** add role administration guardrails ([575aae7](https://github.com/jab16jy/Sistema_Pos/commit/575aae7e9636964168ac6d2e7c090a3d24440bca))
* **auth:** establish role permission foundation ([a37525e](https://github.com/jab16jy/Sistema_Pos/commit/a37525e91fd11e34a3cbad15bfef1106a445fd0d))
* **ui:** wire role permission management ([a122a6b](https://github.com/jab16jy/Sistema_Pos/commit/a122a6bd1ebe3b347fcf5a1cc24957537261b37c))


### Bug Fixes

* **db:** seed system roles and backfill Users.RoleId before RBAC FK ([1f866dd](https://github.com/jab16jy/Sistema_Pos/commit/1f866dd947fa656b70ece38ed7eb5af14b88ac65))
* **ui:** restore module icons in the admin Configuración hub ([e4ff0b0](https://github.com/jab16jy/Sistema_Pos/commit/e4ff0b0547cc621a3c9dd9976b5dddf496c32717))

## [1.6.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.5.1...v1.6.0) (2026-07-12)


### Features

* **ventas:** auditar presentaciones unidad y caja ([632f705](https://github.com/jab16jy/Sistema_Pos/commit/632f705b79adbb8b52045f2e4a797dd12217e0f3))


### Bug Fixes

* **caja:** usar binding OneWay en toggle de presentacion unidad/caja ([fbfecb8](https://github.com/jab16jy/Sistema_Pos/commit/fbfecb8ff06a9615af271bba1bdbbb1338a07eb3))

## [1.5.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.5.0...v1.5.1) (2026-07-12)


### Bug Fixes

* **compras:** cambiar estilo de botón de historial a SecondaryButton y ajustar etiqueta a Ver detalles ([84d66b0](https://github.com/jab16jy/Sistema_Pos/commit/84d66b09079e99feb984a1f2e38aba3877a74a16))

## [1.5.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.4.2...v1.5.0) (2026-07-12)


### Features

* **presentation:** move category grouping button to bottom actions bar and commit active loyalty and purchases changes ([0db8a04](https://github.com/jab16jy/Sistema_Pos/commit/0db8a04f251babb22b78c87cd3ca9745f66f6018))
* **products:** agregar soporte de teclado (Enter) y popup/menú contextual para agrupar productos ([902c459](https://github.com/jab16jy/Sistema_Pos/commit/902c4595f1407db23a0105a809f0fa92c6015902))
* **purchasing:** auditable supplier catalog flow with weighted cost, VAT, quotations, and history ([ffabbde](https://github.com/jab16jy/Sistema_Pos/commit/ffabbdef9399169c404125ec63fd2528afbe7f74))
* **purchasing:** auditable supplier catalog flow with weighted cost, VAT, quotations, and history ([0c73d1a](https://github.com/jab16jy/Sistema_Pos/commit/0c73d1a97579ff9bb148e299db591bb4450bd0c1))
* **purchasing:** catalog-driven supplier purchasing with edit, migration, and tests ([b6e8295](https://github.com/jab16jy/Sistema_Pos/commit/b6e829589422597c61e612391caa3731a0d3a0da))
* **purchasing:** catalog-driven supplier purchasing with edit, migration, and tests ([aa87584](https://github.com/jab16jy/Sistema_Pos/commit/aa87584851e0a256e7ef7dc47734bf7f14fc8d7e))
* **purchasing:** improve purchase feedback and history ([e5d3b1d](https://github.com/jab16jy/Sistema_Pos/commit/e5d3b1dfd3dbfbcc66829b4b437bcf56b4f73dd6))
* **purchasing:** rebuild supplier catalog search UI, allow deletes, fix empty-cart redemption ([b40e3e9](https://github.com/jab16jy/Sistema_Pos/commit/b40e3e9a265fae017d9c120b4f35bf90cb07aeb7))
* **purchasing:** remove supplier quotation sub-feature from Compras ([4d63a4b](https://github.com/jab16jy/Sistema_Pos/commit/4d63a4bf8d12dff0eebff35bae2f74b43f86ba7c))
* **quotes:** reuse cashier's autocomplete search, flag walk-in customers, add no-PDF quote preview ([6da9669](https://github.com/jab16jy/Sistema_Pos/commit/6da9669e5478cd14dc9389fe66f12faa056d4811))
* **reports:** real sales chart grouped by period, readable payment breakdown labels ([9829122](https://github.com/jab16jy/Sistema_Pos/commit/98291229e0542045fc9c7e6905c991dbc973a02e))


### Bug Fixes

* **compras:** ajustar tamaño y etiqueta de botón ver detalle en historial para evitar corte visual ([1421d55](https://github.com/jab16jy/Sistema_Pos/commit/1421d55907f6542e9813c88c995d99ae51a99176))
* **purchasing:** keep history details visible ([0d6efd0](https://github.com/jab16jy/Sistema_Pos/commit/0d6efd0dbcdbfdbe49013b156daf6f2f66444954))
* **purchasing:** make history detail responsive ([bf846b6](https://github.com/jab16jy/Sistema_Pos/commit/bf846b65e890db469854b015eef7a50c4ab13d9f))
* **purchasing:** reconcile catalog-edit branch with shipped quotation/history feature ([dcee398](https://github.com/jab16jy/Sistema_Pos/commit/dcee3985258813f8f5a8f07a61e20a1d8042c5ec))
* **quality:** normalize repository formatting ([a33e88f](https://github.com/jab16jy/Sistema_Pos/commit/a33e88f435a8f2e393423e3d5666da45c1674d30))

## [1.4.2](https://github.com/jab16jy/Sistema_Pos/compare/v1.4.1...v1.4.2) (2026-07-11)


### Bug Fixes

* **cashier:** assign customer on single click in search results ([5b690c6](https://github.com/jab16jy/Sistema_Pos/commit/5b690c6d1d034df4944d62ee6906ae3049c2a274))

## [1.4.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.4.0...v1.4.1) (2026-07-11)


### Bug Fixes

* **cashier:** keep cart item removal visible ([af02100](https://github.com/jab16jy/Sistema_Pos/commit/af021008a5792913eea7ac30342b4403cb9e2cef))
* **quotes:** repair history layout and gate sale conversion by status ([04b977f](https://github.com/jab16jy/Sistema_Pos/commit/04b977f7cc481e8c99fc057786705ef4fc396a41))

## [1.4.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.3.0...v1.4.0) (2026-07-10)


### Features

* **products:** add bulk category assignment use case ([294660f](https://github.com/jab16jy/Sistema_Pos/commit/294660f2606de147898387f780650ff772164690))
* **products:** add bulk category assignment workspace ([8e0dabf](https://github.com/jab16jy/Sistema_Pos/commit/8e0dabfba65e44070dc9cdd0b4b3da8c31504b89))
* **reports:** expand export detail and compact PDFs ([eddcd82](https://github.com/jab16jy/Sistema_Pos/commit/eddcd82ed8eaf3948bf1d0ed2b095948311f0038))

## [1.3.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.2.1...v1.3.0) (2026-07-10)


### Features

* **backup:** implement database restore from a backup file ([824e7d8](https://github.com/jab16jy/Sistema_Pos/commit/824e7d8f2b785751fb6b79e0fb57a94c90ee2364))
* **barcode:** integrate labels into products ([727ee62](https://github.com/jab16jy/Sistema_Pos/commit/727ee62983d7366426a5b6e8d226ce9a3005d8d6))
* **catalog:** enforce PRODUCTS.CategoryId FK with restrictive delete and index ([6e52016](https://github.com/jab16jy/Sistema_Pos/commit/6e520162cde8c836d21d884d73b7276dfb37a115))
* **login:** change error panel color to red on invalid credentials ([647112d](https://github.com/jab16jy/Sistema_Pos/commit/647112d2dbf67d489ccfabfff101c5069689c016))
* **reports:** redesign PDF and Excel exports with KPIs, hierarchy, and cash-digital separation ([3275476](https://github.com/jab16jy/Sistema_Pos/commit/327547664cf2a250a6205afb1fd95fba2fc4b55d))


### Bug Fixes

* **backup:** add functional backup panel to default Settings tab ([6ca1adc](https://github.com/jab16jy/Sistema_Pos/commit/6ca1adc0390692e0ae35635ac5501851afefed03))
* **backup:** create validated SQLite snapshots ([e4be42e](https://github.com/jab16jy/Sistema_Pos/commit/e4be42e237f44bf57d19fbf872e0fba2fc410062))
* **barcode:** harden import validation, serialize lookup, audit migration ([12e40b0](https://github.com/jab16jy/Sistema_Pos/commit/12e40b062d2af6eba2e62426c942c22fe0150d4c))
* **barcode:** harden scanner and migration integrity ([77e232e](https://github.com/jab16jy/Sistema_Pos/commit/77e232ee2dbdde2d93d9ddc8012ac33139dffbb6))
* **reports,backup:** enable PDF/Excel export, fix status overlap, add backup folder config ([86b14b8](https://github.com/jab16jy/Sistema_Pos/commit/86b14b8fd375f98119d30c01b16ef297557370e1))

## [1.2.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.2.0...v1.2.1) (2026-07-10)


### Bug Fixes

* **barcode:** use explicit es-CO culture in label price formatting ([f8b100d](https://github.com/jab16jy/Sistema_Pos/commit/f8b100d7ac3aed0eab083d0a5424007f28b13afe))
* **cashier:** use explicit es-CO culture for decimal formatting ([e0c3e7a](https://github.com/jab16jy/Sistema_Pos/commit/e0c3e7a83236eb005c0fe2c45f89813ecc032d16))
* **loyalty:** use explicit es-CO culture in LoyaltyViewModel formatting ([d4e13d4](https://github.com/jab16jy/Sistema_Pos/commit/d4e13d41fc9ab29b0323f954855e01002cb4e78a))
* **printing,reports:** use explicit es-CO culture in receipt/report formatting ([afb84cc](https://github.com/jab16jy/Sistema_Pos/commit/afb84cc3804a5f4c3f5cf5d0d4c1b1436c8bbc34))

## [1.2.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.1.0...v1.2.0) (2026-07-10)


### Features

* **categories:** implement Category CRUD, UI validations, deletion rules, and ViewModel synchronization ([4335001](https://github.com/jab16jy/Sistema_Pos/commit/43350018148d98b1a872cc3f5fbdb19de8181f7d))
* **login:** implement client-side validations and bind credentials error message feedback ([193238d](https://github.com/jab16jy/Sistema_Pos/commit/193238da3881df8bc8c70f3a793f71adebd11de8))
* **loyalty:** manage tiers and enrich history ([f124c8a](https://github.com/jab16jy/Sistema_Pos/commit/f124c8a178397c2b129ef7feb5ac4e345e656241))
* **products:** box presentation pricing and editable stock ([64dc0fc](https://github.com/jab16jy/Sistema_Pos/commit/64dc0fc467ec2230d8860c8e39b705b0cccb23b3))
* **quotes:** confirm history quote conversion ([3c73be6](https://github.com/jab16jy/Sistema_Pos/commit/3c73be6e039771087f0b3e533473a46bb7938daf))
* **reports:** redirect pending shifts from Reportes straight to Turnos ([4d05533](https://github.com/jab16jy/Sistema_Pos/commit/4d05533abc3da7ca00c3d5557de8e617b3ffd4c1))


### Bug Fixes

* **audit:** add vertical scrollbar to Audit Trail logs and register category filter ([159f626](https://github.com/jab16jy/Sistema_Pos/commit/159f6265fd43750f31b8050f0c833f7045b40764))
* **cashier:** preserve keyboard selections ([c4002d9](https://github.com/jab16jy/Sistema_Pos/commit/c4002d9d809c4de8a4a258dc33d3a8769306ec07))
* **invoices:** add detail preview and loyalty sync ([6ddbed7](https://github.com/jab16jy/Sistema_Pos/commit/6ddbed78303eb378d0772c76a7dff3cd6449abc0))
* **loyalty:** pin tier benefit formatting to es-CO culture ([2006bc0](https://github.com/jab16jy/Sistema_Pos/commit/2006bc0630b024a83482c47788b3eccfc6c6c1da))
* **purchases:** add product search so a purchase can be registered ([241c32a](https://github.com/jab16jy/Sistema_Pos/commit/241c32acaefd4952731eb528b33499d53c88bbe2))
* **reports:** compute profitability aggregates in-memory, not in SQL ([7ee83fb](https://github.com/jab16jy/Sistema_Pos/commit/7ee83fbdd508fd0e6067116724063dd0e5312b0e))
* **shifts:** actually highlight the shift selected from Reportes' redirect ([f1442f5](https://github.com/jab16jy/Sistema_Pos/commit/f1442f511067c12d94055131e86fedd5574d5d3c))
* **shifts:** use local UTC offset in date filter to stop offset crash ([5d544e9](https://github.com/jab16jy/Sistema_Pos/commit/5d544e91bd0aa1e019020909c172416878df556d))
* **whatsapp:** stop leaking local file path into customer message ([c11fd1d](https://github.com/jab16jy/Sistema_Pos/commit/c11fd1d9e26d87e95e2cd4badb78aa72030fbc73))

## [1.1.0](https://github.com/jab16jy/Sistema_Pos/compare/v1.0.1...v1.1.0) (2026-07-09)


### Bug Fixes

* **audit:** close audit-trail gaps and add log pagination ([#98](https://github.com/jab16jy/Sistema_Pos/issues/98)) ([77aa6f6](https://github.com/jab16jy/Sistema_Pos/commit/77aa6f6a9a4afca181992931aeae72d0f7da1119))
* **dashboard:** load caja daily metrics ([#100](https://github.com/jab16jy/Sistema_Pos/issues/100)) ([5522065](https://github.com/jab16jy/Sistema_Pos/commit/552206569e4932e16826283b2ccd8085a27bd542))


### Miscellaneous Chores

* **release:** prepare EPOS 1.1.0 installer ([10a28b3](https://github.com/jab16jy/Sistema_Pos/commit/10a28b39117dcdd4d70fab570fb8b5d5186a6b57))

## [1.0.1](https://github.com/jab16jy/Sistema_Pos/compare/v1.0.0...v1.0.1) (2026-07-09)


### Bug Fixes

* **loyalty:** apply point redemption as a payment tender on the sale ([#96](https://github.com/jab16jy/Sistema_Pos/issues/96)) ([45bc238](https://github.com/jab16jy/Sistema_Pos/commit/45bc238bd51315bafc65ef7b4f3b29dcb4130cce))

## [1.0.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.26.0...v1.0.0) (2026-07-09)


### Features

* **audit:** readable audit log identifiers + invoice list fix ([2a5323b](https://github.com/jab16jy/Sistema_Pos/commit/2a5323b14f40aaba68eb9f804a03bd1dce5b876d))
* **audit:** readable audit log identifiers + invoice list fix ([#93](https://github.com/jab16jy/Sistema_Pos/issues/93)) ([2a5323b](https://github.com/jab16jy/Sistema_Pos/commit/2a5323b14f40aaba68eb9f804a03bd1dce5b876d))
* **domain:** integrate shared contact validation defaults ([522e4db](https://github.com/jab16jy/Sistema_Pos/commit/522e4db8f7acb77337a3b3b8ca7fc3b29b2aad05))

## [0.26.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.25.0...v0.26.0) (2026-07-08)


### Features

* move quote payments from history to cashier ([#90](https://github.com/jab16jy/Sistema_Pos/issues/90)) ([619b4a8](https://github.com/jab16jy/Sistema_Pos/commit/619b4a81db5d1e5db4ce731b7a51481dc950b148))

## [0.25.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.24.0...v0.25.0) (2026-07-08)


### Features

* **documents:** improve receipt pdf and whatsapp attachment flow ([4941c00](https://github.com/jab16jy/Sistema_Pos/commit/4941c006c1e1879e3acf9e0c168b62383f7fbcc3))
* **ui:** auto-load customers view when navigating to clientes module ([9856a7d](https://github.com/jab16jy/Sistema_Pos/commit/9856a7daf6bf73eecce60f8e6d3aefd7c1fdb6fe))
* **ui:** make cashier and shifts side panels scrollable ([6724254](https://github.com/jab16jy/Sistema_Pos/commit/67242549c005bc956d9a5e141f09ed11ca58fd8c))


### Bug Fixes

* **loyalty:** don't crash startup on orphaned Sale.CustomerId ([#88](https://github.com/jab16jy/Sistema_Pos/issues/88)) ([b448dd2](https://github.com/jab16jy/Sistema_Pos/commit/b448dd26552507f87e03a7591a8000b533529999))

## [0.24.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.23.0...v0.24.0) (2026-07-08)


### Features

* **ui:** fix login theme, logo proportions, and desktop app icon ([b56c881](https://github.com/jab16jy/Sistema_Pos/commit/b56c881c1b4d7a5d45c9938e36529e7ee1af0634))
* **ui:** login theme fix, logo proportions & desktop app icon ([3caf885](https://github.com/jab16jy/Sistema_Pos/commit/3caf885f3b5be96b861d72cceb75a29f04d77bad))


### Bug Fixes

* **ui:** enlarge login window height so the button isn't clipped ([7a78e75](https://github.com/jab16jy/Sistema_Pos/commit/7a78e758d89a5adfc993e944e6fd3014d49119ae))

## [0.23.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.22.3...v0.23.0) (2026-07-08)


### Features

* add loyalty program overview and rules management ([fca0359](https://github.com/jab16jy/Sistema_Pos/commit/fca0359c54f5f9ab1d943d7ea53cb6be277a00ce))
* loyalty program overview and rules management ([cec2646](https://github.com/jab16jy/Sistema_Pos/commit/cec264682763305ffdb71f2a6ea6593cb7d17972))

## [0.22.3](https://github.com/jab16jy/Sistema_Pos/compare/v0.22.2...v0.22.3) (2026-07-07)


### Bug Fixes

* correct audit ordering, cashier role gating, and admin auth guards ([dbdc11d](https://github.com/jab16jy/Sistema_Pos/commit/dbdc11d087f339efd79a2bb7699dd42c9a25e01d))
* correct audit ordering, cashier role gating, and admin auth guards ([2dd0602](https://github.com/jab16jy/Sistema_Pos/commit/2dd06022c7101459cac05fbfddfee6daa618ea91))
* correct audit ordering, cashier role gating, and admin auth guards ([defded8](https://github.com/jab16jy/Sistema_Pos/commit/defded8566c28f65a7c1ca51c9b31cf4291ba776))

## [0.22.2](https://github.com/jab16jy/Sistema_Pos/compare/v0.22.1...v0.22.2) (2026-07-06)


### Bug Fixes

* exception hardening across app shell, ViewModels and audit tooling ([#76](https://github.com/jab16jy/Sistema_Pos/issues/76)) ([adec34a](https://github.com/jab16jy/Sistema_Pos/commit/adec34a73851f0e1d05589ccd9de3250130812a5))

## [0.22.1](https://github.com/jab16jy/Sistema_Pos/compare/v0.22.0...v0.22.1) (2026-07-06)


### Bug Fixes

* autocomplete suggestions, SMTP hardening and Gmail send ([85bbe1f](https://github.com/jab16jy/Sistema_Pos/commit/85bbe1fed630ce386a3b66db47919459ac5a649f))
* **caja:** show autocomplete suggestions and refine product lookup ([222be5d](https://github.com/jab16jy/Sistema_Pos/commit/222be5d75294c73bd1da5b2b9f0154ccae7974d3))
* **settings:** protect SMTP password, auto-load config, sanitize errors ([2a2312a](https://github.com/jab16jy/Sistema_Pos/commit/2a2312a1fd24e6563ae78a8d7854ec781ab4a8dc))

## [0.22.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.21.0...v0.22.0) (2026-07-06)


### Features

* persisted SMTP, WhatsApp Desktop/Web, admin invoice cancellation, cashier autocomplete ([#72](https://github.com/jab16jy/Sistema_Pos/issues/72)) ([ab84a42](https://github.com/jab16jy/Sistema_Pos/commit/ab84a42583934d28b9d98f5ebd100df1f2388e8e))

## [0.21.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.20.0...v0.21.0) (2026-07-06)


### Features

* **printer:** auto-detect printer and print by installed name ([#70](https://github.com/jab16jy/Sistema_Pos/issues/70)) ([2433a7f](https://github.com/jab16jy/Sistema_Pos/commit/2433a7f16c50f7ea64c82982242371f8a616a282))

## [0.20.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.19.0...v0.20.0) (2026-07-05)


### Features

* **catalog:** import excel catalog into sqlite ([#68](https://github.com/jab16jy/Sistema_Pos/issues/68)) ([43548e9](https://github.com/jab16jy/Sistema_Pos/commit/43548e9d4e306aa012662878128eb98393ee17f0))

## [0.19.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.18.0...v0.19.0) (2026-07-05)


### Features

* **cashier:** align barcode and receipt checkout flow ([#66](https://github.com/jab16jy/Sistema_Pos/issues/66)) ([1a446fe](https://github.com/jab16jy/Sistema_Pos/commit/1a446fef24b01c845d750fdc925aa4cd096dadd3))

## [0.18.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.17.0...v0.18.0) (2026-07-03)


### Features

* implement desktop logo, sequential F1-F5 shortcuts, mouse wheel navigation and remove wireframe files ([#64](https://github.com/jab16jy/Sistema_Pos/issues/64)) ([ca88963](https://github.com/jab16jy/Sistema_Pos/commit/ca889632d812f11c81a5035698c4669b716072e9))


### Bug Fixes

* **wpf:** update branding assets and XAML parsing ([f4c50d0](https://github.com/jab16jy/Sistema_Pos/commit/f4c50d083904d6cb5c0945b0b5dab5242cdf74c3))

## [0.17.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.16.0...v0.17.0) (2026-07-01)


### Features

* **wpf:** translate reportes view wireframe ([#60](https://github.com/jab16jy/Sistema_Pos/issues/60)) ([289e5c0](https://github.com/jab16jy/Sistema_Pos/commit/289e5c0afe1d073cbd62f2098764aef44b20c6d7)), closes [#59](https://github.com/jab16jy/Sistema_Pos/issues/59)

## [0.16.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.15.0...v0.16.0) (2026-07-01)


### Features

* **wpf:** translate turnos, lealtad, and barcode view wireframes ([#56](https://github.com/jab16jy/Sistema_Pos/issues/56)) ([c1c8da8](https://github.com/jab16jy/Sistema_Pos/commit/c1c8da833bf149d72775887e005f7fc7f94a7b1f)), closes [#55](https://github.com/jab16jy/Sistema_Pos/issues/55)

## [0.15.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.14.0...v0.15.0) (2026-07-01)


### Features

* **wpf:** translate productos, inventario, and compras view wireframes ([#53](https://github.com/jab16jy/Sistema_Pos/issues/53)) ([1cd3da3](https://github.com/jab16jy/Sistema_Pos/commit/1cd3da36db5104e666a37da5310b387021b84cc6))

## [0.14.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.13.0...v0.14.0) (2026-07-01)


### Features

* **wpf:** translate facturas view and configuracion admin hub wireframes ([#51](https://github.com/jab16jy/Sistema_Pos/issues/51)) ([6be4e2e](https://github.com/jab16jy/Sistema_Pos/commit/6be4e2e04b53cc83c0e6468b8ce5b78495e8e03c))

## [0.13.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.12.0...v0.13.0) (2026-07-01)


### Features

* **wpf:** translate clientes and cotizaciones view wireframes ([#48](https://github.com/jab16jy/Sistema_Pos/issues/48)) ([6ed21f8](https://github.com/jab16jy/Sistema_Pos/commit/6ed21f832c6a28b73524b3682e4701c1bb054faf))

## [0.12.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.11.0...v0.12.0) (2026-07-01)


### Features

* **wpf:** translate caja view wireframe ([#44](https://github.com/jab16jy/Sistema_Pos/issues/44)) ([eea82d5](https://github.com/jab16jy/Sistema_Pos/commit/eea82d57db93e561ffcde7e3e2e407f41f2b6e4d))

## [0.11.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.10.1...v0.11.0) (2026-07-01)


### Features

* **wpf:** ship shell theme foundation ([#42](https://github.com/jab16jy/Sistema_Pos/issues/42)) ([9f305b7](https://github.com/jab16jy/Sistema_Pos/commit/9f305b7db19f7ed28bbd3fc05dee29d7c715d911))

## [0.10.1](https://github.com/jab16jy/Sistema_Pos/compare/v0.10.0...v0.10.1) (2026-06-28)


### Bug Fixes

* login text visibility and button activation ([#36](https://github.com/jab16jy/Sistema_Pos/issues/36)) ([ba09006](https://github.com/jab16jy/Sistema_Pos/commit/ba09006923e70bacc19ccc4a6c73674d2c4db99f))

## [0.10.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.9.0...v0.10.0) (2026-06-28)


### Features

* add phase 3 - IVA 19%, CSV import, audit log viewer, cashier shifts ([78d990c](https://github.com/jab16jy/Sistema_Pos/commit/78d990cf465155f6b6cbe781cf3f23972e8e5b1c))
* phase 3 - IVA 19%, CSV import, audit log viewer, cashier shifts ([5c27e41](https://github.com/jab16jy/Sistema_Pos/commit/5c27e41505134860763c0546562fdba3ee05664e))

## [0.9.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.8.1...v0.9.0) (2026-06-28)


### Features

* logout, session lock & User Management module ([638609e](https://github.com/jab16jy/Sistema_Pos/commit/638609e6ab6a7f3e1d948fb3d56d8ef799b9ee9d))

## [0.8.1](https://github.com/jab16jy/Sistema_Pos/compare/v0.8.0...v0.8.1) (2026-06-28)


### Bug Fixes

* populate thermal receipt and invoice PDF with real sale data ([561affd](https://github.com/jab16jy/Sistema_Pos/commit/561affdaad17cb192ce2a3368334173312366a38))
* populate thermal receipt and invoice PDF with real sale data ([84c5256](https://github.com/jab16jy/Sistema_Pos/commit/84c52567afd3accad30564b94c8784076d23614f))

## [0.8.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.7.0...v0.8.0) (2026-06-28)


### Features

* add cash closing UI and date range filters to reports module ([56ee2fd](https://github.com/jab16jy/Sistema_Pos/commit/56ee2fd8cdef104b3424366445196415b1183304))
* cash closing UI + date range filters in reports ([28012b2](https://github.com/jab16jy/Sistema_Pos/commit/28012b261297990ac4ba103b46e24dbd73914c36))

## [0.7.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.6.0...v0.7.0) (2026-06-28)


### Features

* PDF/Excel export for reports module and ESC/POS thermal printer service ([ecb048d](https://github.com/jab16jy/Sistema_Pos/commit/ecb048d303db5920fa2254d2280a9a71676141b6))
* PDF/Excel report export + ESC/POS thermal printer service ([380d679](https://github.com/jab16jy/Sistema_Pos/commit/380d67975a14a034b81e4ab667ff6ca6d2054eb1))

## [0.6.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.5.0...v0.6.0) (2026-06-28)


### Features

* add authentication and role-based access control ([5379cd9](https://github.com/jab16jy/Sistema_Pos/commit/5379cd9fbab21986460fab6296afcc988a3dff48))
* add authentication and role-based access control ([5922aad](https://github.com/jab16jy/Sistema_Pos/commit/5922aadfeeaf971e9a7f67ba23510bff39b8b832))

## [0.5.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.4.0...v0.5.0) (2026-06-28)


### Features

* add loyalty, barcode, profit analytics, categories, and return improvements ([9ac42ce](https://github.com/jab16jy/Sistema_Pos/commit/9ac42ce5785f47526c31c9acfb144c29ef9e53a6))
* **app:** add loyalty, category, profit, return application layer for new-pos-modules ([b25aea4](https://github.com/jab16jy/Sistema_Pos/commit/b25aea416653d052be955e5dfac4df286c9660e1))
* **domain:** add loyalty, barcode, profit domain layer for new-pos-modules ([9ce9b22](https://github.com/jab16jy/Sistema_Pos/commit/9ce9b22b76f9a9c86826d9391b367c8b6122f73f))
* **infra:** add migrations, repositories, profit queries, ZXing barcode, and label printing for new-pos-modules ([f84489a](https://github.com/jab16jy/Sistema_Pos/commit/f84489aa48e5f98217f8b21b8b9556d2c6c6266a))
* **presentation:** add loyalty, barcode, categories UI and analytics for new-pos-modules ([3782312](https://github.com/jab16jy/Sistema_Pos/commit/3782312381d7e71e7a0e9dfcacd9a8b86d84d2ba))


### Bug Fixes

* resolve 3 CRITICAL verification issues for new-pos-modules ([55af168](https://github.com/jab16jy/Sistema_Pos/commit/55af168e0adfcdeedb208db0107936b4d4f615ea))

## [0.4.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.3.0...v0.4.0) (2026-06-27)


### Features

* add runtime gap domain application layer ([42655b0](https://github.com/jab16jy/Sistema_Pos/commit/42655b06322c34fd13ae8a74ee8fd2dcf1a9fc36))
* complete runtime gaps - products, inventory, invoices, settings modules ([3f66673](https://github.com/jab16jy/Sistema_Pos/commit/3f66673d3e0b0ddb8c0313912fa4495428d0ae8e))
* complete runtime gaps - products, inventory, invoices, settings modules ([00199fa](https://github.com/jab16jy/Sistema_Pos/commit/00199fa92962ca84cbb453ab4a3acd35c378b4ab))

## [0.3.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.2.1...v0.3.0) (2026-06-26)


### Features

* **runtime:** add report and print adapters ([6406da0](https://github.com/jab16jy/Sistema_Pos/commit/6406da08139358a4b8b866e0f194dc97a10ee230))
* **runtime:** configure communication and product lookup ([a49aaba](https://github.com/jab16jy/Sistema_Pos/commit/a49aaba9261e577e60b9d00ef473fe15015c935c))
* **runtime:** wire persisted lookup into WPF ([4577e7e](https://github.com/jab16jy/Sistema_Pos/commit/4577e7eb0e192e705dbfa4a5d5f56bcfdd84d9e2))

## [0.2.1](https://github.com/jab16jy/Sistema_Pos/compare/v0.2.0...v0.2.1) (2026-06-26)


### Bug Fixes

* resolve Application namespace ambiguity in App.xaml.cs ([10cca7d](https://github.com/jab16jy/Sistema_Pos/commit/10cca7d598fb35286e1565841bbb627378a27d14))
* **tests:** clear SQLite connection pool before deleting temp db file ([effa8a6](https://github.com/jab16jy/Sistema_Pos/commit/effa8a663edbbf2cc6742ee6983f80915cb1a104))

## [0.2.0](https://github.com/jab16jy/Sistema_Pos/compare/v0.1.0...v0.2.0) (2026-06-26)


### Features

* cashier persistence, dashboard metrics, and quotation UI ([2ff4bf5](https://github.com/jab16jy/Sistema_Pos/commit/2ff4bf592a2cb02b572b94ef3783f1f7bdf5262a))
* cashier persistence, dashboard metrics, and quotation UI ([2ff4bf5](https://github.com/jab16jy/Sistema_Pos/commit/2ff4bf592a2cb02b572b94ef3783f1f7bdf5262a))
* **cashier:** persist sales and real dashboard metrics ([7c42bce](https://github.com/jab16jy/Sistema_Pos/commit/7c42bce0c5296ed7d63322b44279eec901923fe3))
* **cashier:** persist sales and real dashboard metrics ([cc141fe](https://github.com/jab16jy/Sistema_Pos/commit/cc141fe6d9728dc50d81df4f253fad7fa8508cb8))
* **customers:** add customer management module foundation ([cfcf942](https://github.com/jab16jy/Sistema_Pos/commit/cfcf942244142c5a3e4350582c6c05880e6bcac7))
* merge customer-suppliers-reporting-foundation (WU1-WU4) ([6480e97](https://github.com/jab16jy/Sistema_Pos/commit/6480e9768271777327758879781a438428574a2d))
* **quotes:** quotation UI and workflow with contact validation ([590c001](https://github.com/jab16jy/Sistema_Pos/commit/590c0014143e0394eafdfee55a253c9969c90e0e))
* **quotes:** quotation UI and workflow with contact validation ([f459c09](https://github.com/jab16jy/Sistema_Pos/commit/f459c0902d33df9cd54e9afaca1af58dbe6aceaf))
* **receivables:** add customer receivables and cashier settlement foundation ([6708648](https://github.com/jab16jy/Sistema_Pos/commit/6708648ec3a5a337cadcfeb66ba730e71b4a085c))
* **reporting:** add operational reporting and cash closing foundation ([0614f01](https://github.com/jab16jy/Sistema_Pos/commit/0614f0152eeaba93ba7c4f58652eaafde5fa1071))
* **suppliers:** add supplier/purchase module foundation with stable operator session ([2807f12](https://github.com/jab16jy/Sistema_Pos/commit/2807f12d217563d9b2ffbf1150c1f08523058625))
* **ui:** implement initial WPF shell and views with medical aesthetic for Esthetic Medical Store ([9e869f7](https://github.com/jab16jy/Sistema_Pos/commit/9e869f713b8dd59fcaa1ee81241942f86c99c690))
* **ui:** implement WPF shell with medical aesthetics for Esthetic Medical Store ([41a2172](https://github.com/jab16jy/Sistema_Pos/commit/41a21720ccbdee232c993363c27c7c29315b70cf))
* WPF shell, remove Avalonia, agent instructions, CI y release-please ([00df454](https://github.com/jab16jy/Sistema_Pos/commit/00df4544088d38a6d90cb1cc0925aafa903a222b))
* WPF shell, remove Avalonia, agent instructions, CI y release-please ([00df454](https://github.com/jab16jy/Sistema_Pos/commit/00df4544088d38a6d90cb1cc0925aafa903a222b))
* **wpf:** add cashier customer purchase views ([d592617](https://github.com/jab16jy/Sistema_Pos/commit/d5926179b537270a24629053c3039410647f1811))
* **wpf:** add navigation shell ([5ecc5c8](https://github.com/jab16jy/Sistema_Pos/commit/5ecc5c8735a57bf6f6fa733e08c0457e2a201d46))
* **wpf:** add quote receivables report views ([d5fe8d4](https://github.com/jab16jy/Sistema_Pos/commit/d5fe8d467db5d64688e7fc99fce55b7f2f11ca1b))
* **wpf:** wire migration infrastructure ([252e5ad](https://github.com/jab16jy/Sistema_Pos/commit/252e5ad84d9033cc33d1066890f843fa76c65418))


### Bug Fixes

* **ci:** CI solo en PRs, habilitar release-please para crear PRs ([e43c9cb](https://github.com/jab16jy/Sistema_Pos/commit/e43c9cb4e29cf8b5db5b34d46b1cea5f562b1ac9))
* **ci:** run CI only on pull_request, not on push to main ([b2e12e7](https://github.com/jab16jy/Sistema_Pos/commit/b2e12e77567b03203b14f211804a8cebdd156847))
* **shell:** align navigation verification ([e7b7fb2](https://github.com/jab16jy/Sistema_Pos/commit/e7b7fb2d0f886c3512a72a1b1334726602382105))
* **shell:** align navigation verification ([86b459b](https://github.com/jab16jy/Sistema_Pos/commit/86b459b3675f6b18f61ddceb37d326232e445624))
* **ui:** address code review findings on WPF shell ([9779577](https://github.com/jab16jy/Sistema_Pos/commit/9779577a151fcea69c20be1f71ac009908332ccd))
