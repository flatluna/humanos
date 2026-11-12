using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations;

public partial class ExpandEnterpriseBusinessSubjects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @subjects TABLE (Code nvarchar(100), Name nvarchar(200), Description nvarchar(2000), SubjectId uniqueidentifier);

            INSERT INTO @subjects (Code, Name, Description, SubjectId) VALUES
                ('manufactura', 'Manufacturing', 'Production planning, processes, equipment and manufacturing systems.', 'a0000000-0000-0000-0000-00000000000c'),
                ('cadena-de-suministro', 'Supply Chain', 'Sourcing, planning, logistics and end-to-end supply chain management.', 'a0000000-0000-0000-0000-00000000000d'),
                ('calidad', 'Quality', 'Quality systems, audits, continuous improvement and standards.', 'a0000000-0000-0000-0000-00000000000e'),
                ('ingenieria', 'Engineering', 'Engineering design, technical systems and applied problem solving.', 'a0000000-0000-0000-0000-00000000000f'),
                ('construccion', 'Construction', 'Construction planning, project delivery, safety and site operations.', 'a0000000-0000-0000-0000-000000000010'),
                ('bienes-raices', 'Real Estate', 'Property development, transactions, leasing and asset management.', 'a0000000-0000-0000-0000-000000000011'),
                ('seguros', 'Insurance', 'Insurance products, underwriting, claims and risk management.', 'a0000000-0000-0000-0000-000000000012'),
                ('banca', 'Banking', 'Banking products, financial services and regulatory operations.', 'a0000000-0000-0000-0000-000000000013');

            INSERT INTO dbo.Subject (SubjectId, Code, Name, Description, CreatedDate)
            SELECT s.SubjectId, s.Code, s.Name, s.Description, SYSUTCDATETIME()
            FROM @subjects s
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Subject existing WHERE existing.Code = s.Code);

            INSERT INTO dbo.SubjectTranslation (SubjectId, LanguageCode, Name, Description, CreatedDate, UpdatedDate)
            SELECT s.SubjectId, 'en', s.Name, s.Description, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM @subjects s
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.SubjectTranslation existing
                WHERE existing.SubjectId = s.SubjectId AND existing.LanguageCode = 'en'
            );

            INSERT INTO dbo.SubjectTranslation (SubjectId, LanguageCode, Name, Description, CreatedDate, UpdatedDate)
            SELECT s.SubjectId, 'es',
                CASE s.Code
                    WHEN 'manufactura' THEN 'Manufactura'
                    WHEN 'cadena-de-suministro' THEN 'Cadena de Suministro'
                    WHEN 'calidad' THEN 'Calidad'
                    WHEN 'ingenieria' THEN 'Ingeniería'
                    WHEN 'construccion' THEN 'Construcción'
                    WHEN 'bienes-raices' THEN 'Bienes Raíces'
                    WHEN 'seguros' THEN 'Seguros'
                    WHEN 'banca' THEN 'Banca'
                END,
                CASE s.Code
                    WHEN 'manufactura' THEN 'Planeación de producción, procesos, equipos y sistemas de manufactura.'
                    WHEN 'cadena-de-suministro' THEN 'Abastecimiento, planeación, logística y gestión integral de la cadena de suministro.'
                    WHEN 'calidad' THEN 'Sistemas de calidad, auditorías, mejora continua y estándares.'
                    WHEN 'ingenieria' THEN 'Diseño de ingeniería, sistemas técnicos y resolución aplicada de problemas.'
                    WHEN 'construccion' THEN 'Planeación de construcción, entrega de proyectos, seguridad y operaciones en obra.'
                    WHEN 'bienes-raices' THEN 'Desarrollo inmobiliario, transacciones, arrendamiento y gestión de activos.'
                    WHEN 'seguros' THEN 'Productos de seguros, suscripción, siniestros y gestión de riesgos.'
                    WHEN 'banca' THEN 'Productos bancarios, servicios financieros y operaciones regulatorias.'
                END,
                SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM @subjects s
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.SubjectTranslation existing
                WHERE existing.SubjectId = s.SubjectId AND existing.LanguageCode = 'es'
            );

            UPDATE dbo.SubjectTranslation
            SET Name = 'Purchasing', Description = 'Purchasing, sourcing, suppliers and procurement strategy.', UpdatedDate = SYSUTCDATETIME()
            WHERE SubjectId = (SELECT SubjectId FROM dbo.Subject WHERE Code = 'compras') AND LanguageCode = 'en';

            UPDATE dbo.SubjectTranslation
            SET Name = 'Transportation', Description = 'Transportation, fleet, distribution and delivery operations.', UpdatedDate = SYSUTCDATETIME()
            WHERE SubjectId = (SELECT SubjectId FROM dbo.Subject WHERE Code = 'logistica') AND LanguageCode = 'en';

            UPDATE dbo.SubjectTranslation
            SET Name = 'Transporte', Description = 'Transporte, flotillas, distribución y operaciones de entrega.', UpdatedDate = SYSUTCDATETIME()
            WHERE SubjectId = (SELECT SubjectId FROM dbo.Subject WHERE Code = 'logistica') AND LanguageCode = 'es';

            UPDATE dbo.Subject
            SET Name = 'Purchasing', Description = 'Purchasing, sourcing, suppliers and procurement strategy.'
            WHERE Code = 'compras';

            UPDATE dbo.Subject
            SET Name = 'Transportation', Description = 'Transportation, fleet, distribution and delivery operations.'
            WHERE Code = 'logistica';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM dbo.SubjectTranslation
            WHERE SubjectId IN (
                SELECT SubjectId FROM dbo.Subject
                WHERE Code IN ('manufactura', 'cadena-de-suministro', 'calidad', 'ingenieria', 'construccion', 'bienes-raices', 'seguros', 'banca')
            );

            DELETE FROM dbo.Subject
            WHERE Code IN ('manufactura', 'cadena-de-suministro', 'calidad', 'ingenieria', 'construccion', 'bienes-raices', 'seguros', 'banca');
            """);
    }
}
