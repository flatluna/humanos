-- Translate and clean all Spanish entries across catalogs to English and deduplicate

-- 1. Responsibility translations
UPDATE dbo.Responsibility SET Name = 'Support hardware validation and firmware debugging tests.'
WHERE Name LIKE '%Apoyar la validaci%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Collaborate with multidisciplinary engineering teams.'
WHERE Name LIKE '%Colaborar con equipos de%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Coordinate development and implementation of technical initiatives.'
WHERE Name LIKE '%Coordinar el desarrollo e implementaci%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Coordinate technical priorities and project tracking.'
WHERE Name LIKE '%Coordinar prioridades t%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Coordinate hardware design and development projects from concept to final validation.'
WHERE Name LIKE '%Coordinar proyectos de dise%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Define technical guidelines and engineering best practices.'
WHERE Name LIKE '%Definir lineamientos y b%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Document processes, experiments, and technical results.'
WHERE Name LIKE '%Documentar procesos, exp%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Evaluate tools, frameworks, and modern technologies.'
WHERE Name LIKE '%Evaluar herramientas, fr%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Monitor campaign performance metrics and prepare analytical reports.'
WHERE Name LIKE '%Monitorear m%tricas de d%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Participate in architecture and system design definition.'
WHERE Name LIKE '%Participar en la arquite%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Supervise quality, performance, and maintenance of electronic systems.'
WHERE Name LIKE '%Supervisar la calidad, r%' AND IsActive = 1;

UPDATE dbo.Responsibility SET Name = 'Supervise functional, performance, security, and compatibility testing of electronic components.'
WHERE Name LIKE '%Supervisar pruebas funci%' AND IsActive = 1;

-- 2. Tool translations
UPDATE dbo.Tool SET Name = 'Data Visualization Tools'
WHERE Name LIKE '%Herramientas de visualiz%' AND IsActive = 1;

UPDATE dbo.Tool SET Name = 'Cloud AI Services'
WHERE Name LIKE '%Servicios cloud de IA%' AND IsActive = 1;

-- 3. Certification translations
UPDATE dbo.Certification SET Name = 'Machine Learning & AI Certifications'
WHERE Name LIKE '%Certificaciones en Machi%' AND IsActive = 1;

-- 4. Skill translations
UPDATE dbo.Skill SET Name = 'Continuous Learning'
WHERE Name = 'Aprendizaje continuo' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Process Automation'
WHERE Name = 'Automatización de procesos' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Version Control Best Practices'
WHERE Name LIKE 'Buenas pr%cticas de vers%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Coordination Capacity'
WHERE Name = 'Capacidad de coordinación' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Effective Communication'
WHERE Name = 'Comunicación efectiva' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Microcontroller Architecture Knowledge'
WHERE Name LIKE 'Conocimiento de arquitec%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Manufacturing Process Knowledge'
WHERE Name LIKE 'Conocimiento de procesos%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Quality Systems Knowledge'
WHERE Name LIKE 'Conocimiento de sistemas%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Project Coordination'
WHERE Name LIKE 'Coordinaci%n de proyecto%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Machine Learning Model Development'
WHERE Name LIKE 'Desarrollo de modelos de%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Data Pipeline Design & Model Training'
WHERE Name LIKE 'Dise%o de pipelines de d%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Hardware Design & Validation'
WHERE Name LIKE 'Dise%o y validaci%n de h%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Technical Documentation & Standards'
WHERE Name LIKE 'Documentaci%n t%cnica y %' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Technical Report Preparation'
WHERE Name LIKE 'Elaboraci%n de reportes %' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'API & Microservices Integration'
WHERE Name LIKE 'Integraci%n de APIs y se%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Hardware & Firmware Integration'
WHERE Name LIKE 'Integraci%n de hardware %' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Schematics & Diagrams Reading'
WHERE Name LIKE 'Lectura e interpretaci%n%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'AI Libraries & Data Analysis'
WHERE Name LIKE 'Manejo de librer%as de I%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Organization & Prioritization'
WHERE Name = 'Organización y priorización' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Results Orientation'
WHERE Name = 'Orientación a resultados' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Analytical Thinking'
WHERE Name = 'Pensamiento analítico' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Proactivity'
WHERE Name = 'Proactividad' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Data Processing & Analysis'
WHERE Name LIKE 'Procesamiento y an%lisis%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Python Programming'
WHERE Name = 'Programación en Python' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Hardware Testing & Diagnostics'
WHERE Name LIKE 'Pruebas de hardware, dia%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Commercial Writing & Communication'
WHERE Name LIKE 'Redacci%n y comunicaci%n%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Problem Solving'
WHERE Name = 'Resolución de problemas' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Specification Tracking'
WHERE Name LIKE 'Seguimiento de especific%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Collaborative Work'
WHERE Name = 'Trabajo colaborativo' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Model Performance Evaluation & Monitoring'
WHERE Name LIKE 'Evaluaci%n y monitoreo d%' AND IsActive = 1;

UPDATE dbo.Skill SET Name = 'Adaptability'
WHERE Name = 'Adaptabilidad' AND IsActive = 1;

-- 5. Activity & Experience Requirement translations if any
UPDATE dbo.Activity SET Name = 'Review and update content calendars across digital channels.'
WHERE Name LIKE '%calendarios%' AND IsActive = 1;

UPDATE dbo.Activity SET Name = 'Monitor and report campaign performance metrics.'
WHERE Name LIKE '%m%tricas%' AND IsActive = 1;

UPDATE dbo.ExperienceRequirement SET Name = 'Experience in digital marketing and campaign coordination.'
WHERE Name LIKE '%marketing%' AND IsActive = 1;
