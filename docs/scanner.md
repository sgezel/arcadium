De drie scanmodes dienen voor verschillende momenten in de levenscyclus van de lokale bibliotheek:

   Mode      Wanneer                Bedoeld gedrag
  ━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   init      Eerste installatie     Maak/vul de SQLite-bibliotheek via een volledige scan van alle geconfigureerde
                                    systemen.
  ────────  ─────────────────────  ─────────────────────────────────────────────────────────────────────────────────
   update    Dagelijks onderhoud    Scan opnieuw, maar verwerk alleen nieuwe of gewijzigde ROMs; markeer ROMs die
                                    niet meer gevonden worden als verwijderd.
  ────────  ─────────────────────  ─────────────────────────────────────────────────────────────────────────────────
   verify    Diagnose/controle      Vergelijk de database volledig met de schijf en rapporteer afwijkingen, zonder
                                    normaal gesproken data te wijzigen.

  Concreet:

  arcadium-scanner scan init --db data/arcadium.db
  arcadium-scanner scan update --db data/arcadium.db
  arcadium-scanner scan verify --db data/arcadium.db

  update kan met grootte en wijzigingstijd bepalen of een ROM veranderd is. verify is nuttig na handmatige
  verplaatsingen, een herstelde backup, of wanneer de UI vreemde ontbrekende games/media toont.

  De namen bestaan al in src/Arcadium.Core/Models/ScanMode.cs, maar de scanner meldt momenteel expliciet dat ze
  nog niet geïmplementeerd zijn. De roadmap beschrijft al vooral het gedrag van update: gewijzigde ROMs upserten en
  niet-teruggevonden ROMs als deleted markeren.
