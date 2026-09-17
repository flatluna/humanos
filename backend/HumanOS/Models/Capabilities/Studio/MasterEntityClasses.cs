namespace HumanOS.Models.Capabilities.Studio;

public sealed class MasterEntityClassDefinition
{
    public string Code { get; set; } = string.Empty;
    public string NameEs { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescriptionEs { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Canonical, closed taxonomy of 50 standard domain entity classes (bilingual ES/EN).
/// Used by StructuredLearningEntityExtractionAgent to ensure consistent, non-arbitrary
/// entity typing across legal, technical, business, academic, and general courses.
/// </summary>
public static class MasterEntityClasses
{
    public static readonly IReadOnlyList<MasterEntityClassDefinition> All =
    [
        // Legal & Normative
        new() { Code = "LegalArticle", NameEs = "Artículo de Ley / Reglamento", NameEn = "Legal Article / Provision", DescriptionEs = "Disposición normativa o artículo específico (ej. Artículo 1, Art. 12, Artículo 17 de la Ley).", DescriptionEn = "Specific statutory provision or article (e.g., Article 1, Art. 12, Article 17 of the Law).", Category = "Legal" },
        new() { Code = "Law", NameEs = "Ley / Código / Reglamento", NameEn = "Law / Code / Regulation", DescriptionEs = "Cuerpo normativo formal (ej. Ley Antilavado, Código de Comercio, Reglamento del SAT).", DescriptionEn = "Enacted legal statute or regulatory code.", Category = "Legal" },
        new() { Code = "LegalChapter", NameEs = "Capítulo / Título Legal", NameEn = "Legal Chapter / Title", DescriptionEs = "División estructural de una ley o código (ej. Capítulo II, Título Tercero).", DescriptionEn = "Structural subdivision of a legal statute.", Category = "Legal" },
        new() { Code = "LegalFraction", NameEs = "Fracción / Inciso Legal", NameEn = "Legal Fraction / Subsection", DescriptionEs = "Subdivisión formal de un artículo normativo.", DescriptionEn = "Formal subsection or clause within an article.", Category = "Legal" },
        new() { Code = "Sanction", NameEs = "Sanción / Penalización", NameEn = "Sanction / Penalty", DescriptionEs = "Castigo o consecuencia punitiva formal (multa, revocación, clausura).", DescriptionEn = "Punitive consequence or administrative fine.", Category = "Legal" },
        new() { Code = "Obligation", NameEs = "Obligación Legal", NameEn = "Legal Obligation", DescriptionEs = "Deber jurídico exigible establecido por norma.", DescriptionEn = "Legally binding duty or compliance requirement.", Category = "Legal" },
        new() { Code = "Right", NameEs = "Derecho Legal / Atribución", NameEn = "Legal Right / Power", DescriptionEs = "Facultad o prerrogativa legal reconocida.", DescriptionEn = "Enforceable entitlement or administrative power.", Category = "Legal" },
        new() { Code = "AuthorityRole", NameEs = "Autoridad / Ente Regulador", NameEn = "Authority / Regulator", DescriptionEs = "Rol de autoridad pública u órgano sancionador (ej. Juez, Agente MP).", DescriptionEn = "Public officer or regulatory decision-maker.", Category = "Legal" },
        new() { Code = "Crime", NameEs = "Delito / Infracción Penal", NameEn = "Crime / Offense", DescriptionEs = "Conducta ilícita tipificada penalmente (ej. Lavado de dinero, Fraude).", DescriptionEn = "Statutory criminal offense or illicit activity.", Category = "Legal" },
        new() { Code = "OfficialPublication", NameEs = "Publicación Oficial / DOF", NameEn = "Official Publication / Gazette", DescriptionEs = "Gaceta, diario oficial o decreto vinculante.", DescriptionEn = "Official gazette, decree, or promulgated text.", Category = "Legal" },

        // Geography & Locations
        new() { Code = "City", NameEs = "Ciudad", NameEn = "City", DescriptionEs = "Entidad municipal o urbe (ej. Seattle, Monterrey, Madrid).", DescriptionEn = "Incorporated city, municipality, or urban center.", Category = "Geography" },
        new() { Code = "Country", NameEs = "País / Jurisdicción", NameEn = "Country / Jurisdiction", DescriptionEs = "Estado soberano o territorio nacional (ej. México, España, EE. UU.).", DescriptionEn = "Sovereign nation or federal territory.", Category = "Geography" },
        new() { Code = "Place", NameEs = "Lugar / Sede / Recinto", NameEn = "Place / Facility / Venue", DescriptionEs = "Ubicación física específica (ej. Aduana marítima, Oficinas centrales).", DescriptionEn = "Specific geographic or physical facility.", Category = "Geography" },

        // Physical, Nature & Materials
        new() { Code = "Material", NameEs = "Material / Sustancia", NameEn = "Material / Substance", DescriptionEs = "Materia prima, químico o mineral (ej. Acero, Cemento, Oro, Petróleo).", DescriptionEn = "Raw material, substance, chemical, or metal (e.g., Steel, Cement, Gold).", Category = "Physical" },
        new() { Code = "Food", NameEs = "Alimento / Producto Agrícola", NameEn = "Food / Agricultural Product", DescriptionEs = "Fruta, vegetal, grano o preparado comestible (ej. Pera, Café, Trigo).", DescriptionEn = "Foodstuff, produce, crop, or beverage (e.g., Pear, Coffee, Wheat).", Category = "Physical" },
        new() { Code = "Animal", NameEs = "Animal / Especie", NameEn = "Animal / Species", DescriptionEs = "Ser vivo del reino animal (ej. Equino, Bovino, Canino).", DescriptionEn = "Fauna, livestock, or wildlife specimen.", Category = "Physical" },
        new() { Code = "Plant", NameEs = "Planta / Flora", NameEn = "Plant / Flora", DescriptionEs = "Especie vegetal o recurso botánico (ej. Agave, Cedro).", DescriptionEn = "Botanical species, flora, or crop.", Category = "Physical" },
        new() { Code = "PhysicalObject", NameEs = "Objeto Físico / Dispositivo", NameEn = "Physical Object / Device", DescriptionEs = "Bien tangible, equipo o artefacto físico.", DescriptionEn = "Tangible equipment, hardware token, or article.", Category = "Physical" },
        new() { Code = "Asset", NameEs = "Activo / Bien Patrimonial", NameEn = "Asset / Property", DescriptionEs = "Bien con valor económico (inmuebles, vehículos, obras de arte).", DescriptionEn = "Real estate, vehicle, jewelry, or tangible economic asset.", Category = "Finance" },
        new() { Code = "Commodity", NameEs = "Mercancía / Producto Comercial", NameEn = "Commodity / Merchandise", DescriptionEs = "Bienes muebles objeto de comercio exterior o local.", DescriptionEn = "Commercial goods, freight, or fungible commodities.", Category = "Business" },

        // Organizations & Roles
        new() { Code = "Organization", NameEs = "Organización / Institución", NameEn = "Organization / Company", DescriptionEs = "Persona moral, empresa, cámara u organismo no gubernamental (ej. SAT, UIF).", DescriptionEn = "Corporation, NGO, confederation, or agency.", Category = "Organizations" },
        new() { Code = "GovernmentBody", NameEs = "Órgano de Gobierno / Secretaría", NameEn = "Government Body / Ministry", DescriptionEs = "Secretaría de Estado o dependencia gubernamental.", DescriptionEn = "Government ministry, commission, or state department.", Category = "Organizations" },
        new() { Code = "PersonRole", NameEs = "Rol de Persona / Puesto", NameEn = "Person Role / Position", DescriptionEs = "Calidad jurídica o puesto que desempeña un individuo (ej. Contribuyente, Oficial de Cumplimiento).", DescriptionEn = "Functional or legal capacity held by a person.", Category = "People" },
        new() { Code = "Person", NameEs = "Persona Individual", NameEn = "Individual Person", DescriptionEs = "Individuo específico identificado por nombre propio.", DescriptionEn = "Specific natural person identified by proper name.", Category = "People" },

        // Economy & Finance
        new() { Code = "FinancialInstrument", NameEs = "Instrumento Financiero", NameEn = "Financial Instrument", DescriptionEs = "Medio o título de pago (cheque, monedero electrónico, bono, pagaré).", DescriptionEn = "Stored-value card, cashier check, draft, or promissory note.", Category = "Finance" },
        new() { Code = "FinancialConcept", NameEs = "Concepto Financiero", NameEn = "Financial Concept", DescriptionEs = "Término o principio económico (tasa de interés, amortización, liquidez).", DescriptionEn = "Financial or economic concept (interest rate, amortization).", Category = "Finance" },
        new() { Code = "Currency", NameEs = "Moneda / Divisa", NameEn = "Currency", DescriptionEs = "Signo monetario de curso legal o virtual (Pesos, USD, EUR, Cripto).", DescriptionEn = "Legal tender or virtual currency.", Category = "Finance" },
        new() { Code = "Threshold", NameEs = "Umbral / Límite Cuantitativo", NameEn = "Threshold / Quantitative Limit", DescriptionEs = "Monto, porcentaje o medida límite que detona efectos (ej. 3,210 UMAS, 10,000 USD).", DescriptionEn = "Quantitative boundary or statutory threshold that triggers rules.", Category = "Finance" },
        new() { Code = "TimeLimit", NameEs = "Plazo / Término Temporal", NameEn = "Time Limit / Deadline", DescriptionEs = "Periodo legal o término calendario (ej. 10 días hábiles, 5 años).", DescriptionEn = "Statutory period, retention window, or calendar deadline.", Category = "General" },
        new() { Code = "Date", NameEs = "Fecha Específica", NameEn = "Specific Date", DescriptionEs = "Punto en el tiempo del calendario (ej. 27 de marzo de 2026).", DescriptionEn = "Specific calendar date or historic day.", Category = "General" },

        // Operations, Process & Governance
        new() { Code = "Process", NameEs = "Proceso / Flujo", NameEn = "Process / Workflow", DescriptionEs = "Conjunto secuencial de actividades orientadas a un fin.", DescriptionEn = "Structured sequence of steps or organizational process.", Category = "Operations" },
        new() { Code = "Activity", NameEs = "Actividad Operativa o Regulada", NameEn = "Activity / Operation", DescriptionEs = "Práctica o acto formal (ej. Actividad Vulnerable, Compraventa de vehículos).", DescriptionEn = "Regulated operation or business activity.", Category = "Operations" },
        new() { Code = "Service", NameEs = "Servicio", NameEn = "Service", DescriptionEs = "Prestación intangible profesional o comercial (notarial, asesoría).", DescriptionEn = "Professional or intangible commercial service.", Category = "Operations" },
        new() { Code = "Risk", NameEs = "Riesgo", NameEn = "Risk", DescriptionEs = "Amenaza o contingencia cuantificable (riesgo operativo, reputacional).", DescriptionEn = "Identified vulnerability, exposure, or compliance risk.", Category = "Operations" },
        new() { Code = "Condition", NameEs = "Condición / Supuesto", NameEn = "Condition / Prerequisite", DescriptionEs = "Hipótesis fáctica o requisito que debe cumplirse.", DescriptionEn = "Prerequisite condition or factual trigger.", Category = "Operations" },
        new() { Code = "Metric", NameEs = "Métrica / Indicador", NameEn = "Metric / KPI", DescriptionEs = "Medida de desempeño o control.", DescriptionEn = "Quantitative indicator or compliance metric.", Category = "Operations" },
        new() { Code = "Standard", NameEs = "Estándar / Norma Técnica", NameEn = "Standard / Specification", DescriptionEs = "Pauta técnica o internacional (ej. ISO 27001, Estándares GAFI).", DescriptionEn = "Technical specification or international benchmark.", Category = "Operations" },
        new() { Code = "Event", NameEs = "Evento / Sesión", NameEn = "Event / Session", DescriptionEs = "Acontecimiento temporal (auditoría, visita de verificación).", DescriptionEn = "Scheduled proceeding, audit, or assembly.", Category = "Operations" },

        // Technology & Data
        new() { Code = "System", NameEs = "Sistema / Plataforma", NameEn = "System / Software Platform", DescriptionEs = "Plataforma tecnológica o infraestructura digital (Portal SPID).", DescriptionEn = "Software application, platform, or automated system.", Category = "Technology" },
        new() { Code = "Database", NameEs = "Base de Datos / Padrón / Registro", NameEn = "Database / Registry", DescriptionEs = "Repositorio formal de registros (Padrón de Actividades Vulnerables).", DescriptionEn = "Official registry, index, or structured repository.", Category = "Technology" },
        new() { Code = "Document", NameEs = "Documento", NameEn = "Document", DescriptionEs = "Instrumento escrito o archivo formal (Dictamen, Declaración, Reporte).", DescriptionEn = "Formal instrument, record, or certificate.", Category = "Technology" },
        new() { Code = "Certificate", NameEs = "Certificado / Credencial", NameEn = "Certificate / Credential", DescriptionEs = "Constancia formal o firma electrónica (e.firma, FIEL).", DescriptionEn = "Digital credential, seal, or official certificate.", Category = "Technology" },
        new() { Code = "CommunicationMethod", NameEs = "Medio de Comunicación / Notificación", NameEn = "Communication / Notice Method", DescriptionEs = "Canal formal para remitir avisos (Buzón tributario, correo certificado).", DescriptionEn = "Authorized dispatch mechanism or formal notice channel.", Category = "Technology" },
        new() { Code = "Tool", NameEs = "Herramienta", NameEn = "Tool", DescriptionEs = "Utensilio, software aplicativo o instrumento auxiliar.", DescriptionEn = "Utility, device, or software instrument.", Category = "Technology" },

        // Science & Mathematics
        new() { Code = "ScientificPrinciple", NameEs = "Principio Científico / Teorema", NameEn = "Scientific Principle / Theorem", DescriptionEs = "Ley física, teorema comprobado o postulado científico.", DescriptionEn = "Empirical law, proven theorem, or scientific postulate.", Category = "Science" },
        new() { Code = "MathematicalConcept", NameEs = "Concepto Matemático", NameEn = "Mathematical Concept", DescriptionEs = "Noción abstracta cuantitativa (Derivada, Matriz, Variable).", DescriptionEn = "Mathematical concept, variable, or domain.", Category = "Science" },
        new() { Code = "MathematicalExpression", NameEs = "Expresión Matemática / Fórmula", NameEn = "Mathematical Expression / Formula", DescriptionEs = "Ecuación o expresión matemática completa (ej. i = √−1).", DescriptionEn = "Complete mathematical equation or formal expression.", Category = "Science" },
        new() { Code = "Quantity", NameEs = "Cantidad / Dimensión", NameEn = "Quantity / Dimension", DescriptionEs = "Unidad de medida física o cuantía exacta.", DescriptionEn = "Physical unit of measurement or exact magnitude.", Category = "Science" },

        // General
        new() { Code = "GeneralRule", NameEs = "Regla General / Criterio", NameEn = "General Rule / Guideline", DescriptionEs = "Directriz general no tipificada como artículo individual.", DescriptionEn = "Broad guideline or governing rule.", Category = "General" },
        new() { Code = "GeneralConcept", NameEs = "Concepto General", NameEn = "General Concept", DescriptionEs = "Idea o concepto teórico que no encaja en las clases anteriores.", DescriptionEn = "Abstract theoretical concept not covered by specialized classes.", Category = "General" }
    ];

    private static readonly Dictionary<string, MasterEntityClassDefinition> ByCode =
        All.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public static MasterEntityClassDefinition? Find(string code) =>
        ByCode.TryGetValue(code, out var def) ? def : null;

    /// <summary>
    /// Normalizes historical/synonymous entity type strings to the canonical MasterEntityClasses code.
    /// E.g. "LawArticle", "LegalProvision", "LegalReference", "Article" -> "LegalArticle"
    /// </summary>
    public static string NormalizeEntityType(string rawType, string? canonicalName = null)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "GeneralConcept";

        var clean = rawType.Trim();

        // If the entity name itself is clearly a legal article (e.g. "Artículo 12", "Art. 17"),
        // force LegalArticle regardless of what raw type was generated.
        if (!string.IsNullOrWhiteSpace(canonicalName))
        {
            var cleanName = canonicalName.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanName, @"^(art[ií]culo|art\.)\s+\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return "LegalArticle";
            }
        }

        return clean.ToLowerInvariant() switch
        {
            "legalarticle" or "lawarticle" or "article" or "legalprovision" or "legalreference" or "artículo" or "articulo" or "legalarticlepart" => "LegalArticle",
            "law" or "ley" or "reglamento" or "codigo" or "código" or "legalcode" or "legislation" or "regulation" or "legaldocument" or "regulatoryinstrument" or "ruleset" => "Law",
            "legalchapter" or "chapter" or "capitulo" or "capítulo" or "legalcodechapter" or "legalsection" => "LegalChapter",
            "legalfraction" or "fraccion" or "fracción" or "inciso" or "legalsubdivision" or "legalprovisionpart" or "legalreferencepart" => "LegalFraction",
            "sanction" or "sancion" or "sanción" or "penalty" or "multa" or "administrativesanction" => "Sanction",
            "obligation" or "obligacion" or "obligación" or "legalobligation" or "compliancemechanism" => "Obligation",
            "right" or "derecho" or "legalpower" => "Right",
            "authorityrole" or "authority" or "autoridad" or "regulatoryauthority" => "AuthorityRole",
            "crime" or "delito" or "criminaloffense" or "crimetype" or "illicitactivity" or "legaloffense" or "offense" => "Crime",
            "officialpublication" or "dof" or "diariooficial" or "officialgazette" or "gazette" or "officialgazetteentry" => "OfficialPublication",

            "city" or "ciudad" => "City",
            "country" or "pais" or "país" or "jurisdiction" or "jurisdiccion" or "jurisdicción" => "Country",
            "place" or "lugar" or "recinto" or "ubicacion" or "ubicación" or "address" or "domicilio" or "administrativedivision" => "Place",

            "material" or "substance" or "sustancia" or "mineral" or "preciousmetal" or "metal" => "Material",
            "food" or "alimento" or "comida" or "agriculturalproduct" => "Food",
            "animal" or "especie" or "fauna" => "Animal",
            "plant" or "planta" or "flora" or "vegetal" => "Plant",
            "physicalobject" or "object" or "objeto" or "device" or "dispositivo" => "PhysicalObject",
            "asset" or "activo" or "bien" or "patrimonio" or "realestate" or "inmueble" => "Asset",
            "commodity" or "mercancia" or "mercancía" or "merchandise" or "goods" or "good" => "Commodity",

            "organization" or "organizacion" or "organización" or "institucion" or "institution" or "company" or "empresa" or "organizationalunit" or "organizationalbody" or "socialgroup" or "legalentity" or "legalentitytype" => "Organization",
            "governmentbody" or "governmentagency" or "secretaria" or "secretaría" or "ministerio" or "administrativebody" or "administrativeunit" or "institutionalorgan" => "GovernmentBody",
            "personrole" or "role" or "rol" or "puesto" or "cargo" or "persontype" or "organizationrole" => "PersonRole",
            "person" or "persona" or "individual" or "autor" or "titular" => "Person",

            "financialinstrument" or "instrumentofinanciero" or "monetaryinstrument" or "paymentmethod" or "financialstructure" => "FinancialInstrument",
            "financialconcept" or "conceptofinanciero" or "economicresource" or "financialresource" or "monetaryresource" or "monetaryconcept" or "payment" or "paymentinstallment" => "FinancialConcept",
            "currency" or "divisa" or "moneda" or "money" or "dinero" => "Currency",
            "threshold" or "umbral" or "limite" or "límite" or "customsvalue" => "Threshold",
            "timelimit" or "plazo" or "termino" or "término" or "timeperiod" or "reportingperiod" => "TimeLimit",
            "date" or "fecha" or "eventdate" or "publicationdate" or "dateconcept" => "Date",

            "process" or "proceso" or "legalprocess" or "administrativeprocedure" or "legalprocedure" or "administrativeaction" or "procedure" or "procedimiento" => "Process",
            "activity" or "actividad" or "legalactivity" or "verificationactivity" or "trade" => "Activity",
            "service" or "servicio" => "Service",
            "risk" or "riesgo" or "contingencia" or "threat" => "Risk",
            "condition" or "condicion" or "condición" or "legalcondition" or "requirement" or "requisito" or "legalconsequence" or "legaldecision" or "legaldetermination" => "Condition",
            "metric" or "metrica" or "métrica" or "kpi" or "indicador" => "Metric",
            "standard" or "estandar" or "estándar" or "normatecnica" or "norma" => "Standard",
            "event" or "evento" or "legalevent" or "audit" or "inspeccion" or "inspección" => "Event",

            "system" or "sistema" or "platform" or "plataforma" or "electronicsystem" or "informationsystem" or "application" => "System",
            "database" or "basededatos" or "padron" or "padrón" or "registry" or "registro" or "dataregistry" or "administrativeregister" or "register" => "Database",
            "document" or "documento" or "report" or "reporte" or "documenttype" or "documentformat" or "documentcopy" or "documentsection" or "documentmeta" or "form" or "administrativeorder" or "agreement" or "annex" or "auditreport" or "notification" or "administrativenotice" or "administrativerecord" or "legalnotice" or "resolution" or "legalinstrument" or "financialagreement" or "publication" or "legalact" or "legalform" or "legaldefinition" or "legalcitation" => "Document",
            "certificate" or "certificado" or "credential" or "credencial" or "electronicsignature" or "seal" or "seallegend" => "Certificate",
            "communicationmethod" or "mediodecomunicacion" or "comunicacion" or "notificacion" => "CommunicationMethod",
            "tool" or "herramienta" => "Tool",

            "scientificprinciple" or "principio" or "teorema" or "theorem" or "hypothesis" => "ScientificPrinciple",
            "mathematicalconcept" or "matematicas" or "matemáticas" or "formula" => "MathematicalConcept",
            "mathematicalexpression" or "expresion" or "expresión" or "formulae" => "MathematicalExpression",
            "quantity" or "cantidad" or "dimension" or "dimensión" or "number" or "numero" or "número" => "Quantity",

            "generalrule" or "regla" or "reglageneral" or "administrativerule" or "legalrule" or "administrativerequest" or "administrativemeasure" => "GeneralRule",
            _ => "GeneralConcept"
        };
    }
}
