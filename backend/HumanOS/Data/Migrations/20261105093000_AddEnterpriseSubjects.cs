using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanOS.Data.Migrations;

public partial class AddEnterpriseSubjects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @subjects TABLE (Code nvarchar(100), Name nvarchar(200), Description nvarchar(2000), SubjectId uniqueidentifier);

            INSERT INTO @subjects (Code, Name, Description, SubjectId) VALUES
                ('inventario', 'Inventory', 'Stock control, warehouse operations and inventory planning.', 'a0000000-0000-0000-0000-000000000001'),
                ('gestion', 'Management', 'Leadership, administration and organizational management.', 'a0000000-0000-0000-0000-000000000002'),
                ('operaciones', 'Operations', 'Business operations, processes and service delivery.', 'a0000000-0000-0000-0000-000000000003'),
                ('compras', 'Procurement', 'Purchasing, sourcing, suppliers and procurement strategy.', 'a0000000-0000-0000-0000-000000000004'),
                ('ventas', 'Sales', 'Sales strategy, account management and commercial execution.', 'a0000000-0000-0000-0000-000000000005'),
                ('marketing', 'Marketing', 'Marketing strategy, customers, brands and campaigns.', 'a0000000-0000-0000-0000-000000000006'),
                ('contabilidad', 'Accounting', 'Accounting, financial reporting and bookkeeping.', 'a0000000-0000-0000-0000-000000000007'),
                ('servicio-al-cliente', 'Customer Service', 'Customer support, service quality and experience.', 'a0000000-0000-0000-0000-000000000008'),
                ('logistica', 'Logistics', 'Transportation, distribution and supply chain coordination.', 'a0000000-0000-0000-0000-000000000009'),
                ('cumplimiento', 'Compliance', 'Policies, controls, risk and regulatory compliance.', 'a0000000-0000-0000-0000-00000000000a'),
                ('gestion-de-proyectos', 'Project Management', 'Planning, delivery, collaboration and project governance.', 'a0000000-0000-0000-0000-00000000000b');

            INSERT INTO dbo.Subject (SubjectId, Code, Name, Description, CreatedDate)
            SELECT s.SubjectId, s.Code, s.Name, s.Description, SYSUTCDATETIME()
            FROM @subjects s
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Subject existing WHERE existing.Code = s.Code);

            INSERT INTO dbo.SubjectTranslation (SubjectId, LanguageCode, Name, Description, CreatedDate, UpdatedDate)
            SELECT s.SubjectId, 'en', s.Name, s.Description, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM @subjects s
            WHERE EXISTS (SELECT 1 FROM dbo.Subject existing WHERE existing.Code = s.Code)
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.SubjectTranslation existing
                  WHERE existing.SubjectId = s.SubjectId AND existing.LanguageCode = 'en'
              );

            INSERT INTO dbo.SubjectTranslation (SubjectId, LanguageCode, Name, Description, CreatedDate, UpdatedDate)
            SELECT s.SubjectId, 'es',
                CASE s.Code
                    WHEN 'inventario' THEN 'Inventario'
                    WHEN 'gestion' THEN 'Gestión'
                    WHEN 'operaciones' THEN 'Operaciones'
                    WHEN 'compras' THEN 'Compras'
                    WHEN 'ventas' THEN 'Ventas'
                    WHEN 'marketing' THEN 'Marketing'
                    WHEN 'contabilidad' THEN 'Contabilidad'
                    WHEN 'servicio-al-cliente' THEN 'Servicio al Cliente'
                    WHEN 'logistica' THEN 'Logística'
                    WHEN 'cumplimiento' THEN 'Cumplimiento'
                    WHEN 'gestion-de-proyectos' THEN 'Gestión de Proyectos'
                END,
                CASE s.Code
                    WHEN 'inventario' THEN 'Control de existencias, operaciones de almacén y planificación de inventario.'
                    WHEN 'gestion' THEN 'Liderazgo, administración y gestión organizacional.'
                    WHEN 'operaciones' THEN 'Operaciones empresariales, procesos y entrega de servicios.'
                    WHEN 'compras' THEN 'Adquisiciones, abastecimiento, proveedores y estrategia de compras.'
                    WHEN 'ventas' THEN 'Estrategia comercial, gestión de cuentas y ejecución de ventas.'
                    WHEN 'marketing' THEN 'Estrategia de marketing, clientes, marcas y campañas.'
                    WHEN 'contabilidad' THEN 'Contabilidad, reportes financieros y registro contable.'
                    WHEN 'servicio-al-cliente' THEN 'Atención al cliente, calidad del servicio y experiencia.'
                    WHEN 'logistica' THEN 'Transporte, distribución y coordinación de la cadena de suministro.'
                    WHEN 'cumplimiento' THEN 'Políticas, controles, riesgos y cumplimiento normativo.'
                    WHEN 'gestion-de-proyectos' THEN 'Planeación, entrega, colaboración y gobierno de proyectos.'
                END,
                SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM @subjects s
            WHERE EXISTS (SELECT 1 FROM dbo.Subject existing WHERE existing.Code = s.Code)
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.SubjectTranslation existing
                  WHERE existing.SubjectId = s.SubjectId AND existing.LanguageCode = 'es'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM dbo.SubjectTranslation
            WHERE SubjectId IN (
                SELECT SubjectId FROM dbo.Subject
                WHERE Code IN ('inventario', 'gestion', 'operaciones', 'compras', 'ventas', 'marketing', 'contabilidad', 'servicio-al-cliente', 'logistica', 'cumplimiento', 'gestion-de-proyectos')
            );

            DELETE FROM dbo.Subject
            WHERE Code IN ('inventario', 'gestion', 'operaciones', 'compras', 'ventas', 'marketing', 'contabilidad', 'servicio-al-cliente', 'logistica', 'cumplimiento', 'gestion-de-proyectos');
            """);
    }
}
