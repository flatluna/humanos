IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'engrams-humanos-backend')
BEGIN
    DROP USER [engrams-humanos-backend];
END
CREATE USER [engrams-humanos-backend] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [engrams-humanos-backend];
ALTER ROLE db_datawriter ADD MEMBER [engrams-humanos-backend];
