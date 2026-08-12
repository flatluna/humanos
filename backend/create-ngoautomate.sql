-- Crear base de datos NGOAutomate en Azure SQL
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'NGOAutomate')
BEGIN
    CREATE DATABASE [NGOAutomate]
END
GO

USE [NGOAutomate]
GO

-- Tabla: Organizations (ONGs)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Organizations')
BEGIN
    CREATE TABLE Organizations (
        OrgId INT PRIMARY KEY IDENTITY(1,1),
        OrgName NVARCHAR(255) NOT NULL,
        Country NVARCHAR(100),
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        IsActive BIT DEFAULT 1
    )
END
GO

-- Tabla: Agents (Agentes AI)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Agents')
BEGIN
    CREATE TABLE Agents (
        AgentId INT PRIMARY KEY IDENTITY(1,1),
        OrgId INT NOT NULL,
        AgentName NVARCHAR(255) NOT NULL,
        AgentType NVARCHAR(50),
        Instructions NVARCHAR(MAX),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        FOREIGN KEY (OrgId) REFERENCES Organizations(OrgId)
    )
END
GO

-- Tabla: Tasks (Tareas automatizadas)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tasks')
BEGIN
    CREATE TABLE Tasks (
        TaskId INT PRIMARY KEY IDENTITY(1,1),
        AgentId INT NOT NULL,
        TaskName NVARCHAR(255) NOT NULL,
        TaskStatus NVARCHAR(50),
        InputData NVARCHAR(MAX),
        OutputData NVARCHAR(MAX),
        ExecutedAt DATETIME,
        CompletedAt DATETIME,
        FOREIGN KEY (AgentId) REFERENCES Agents(AgentId)
    )
END
GO

-- Tabla: Logs (Auditoría)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Logs')
BEGIN
    CREATE TABLE Logs (
        LogId BIGINT PRIMARY KEY IDENTITY(1,1),
        OrgId INT,
        AgentId INT,
        TaskId INT,
        LogLevel NVARCHAR(20),
        Message NVARCHAR(MAX),
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        FOREIGN KEY (OrgId) REFERENCES Organizations(OrgId),
        FOREIGN KEY (AgentId) REFERENCES Agents(AgentId),
        FOREIGN KEY (TaskId) REFERENCES Tasks(TaskId)
    )
END
GO
