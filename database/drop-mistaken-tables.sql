-- =============================================================================================
--  CLEAN-UP - extra step, only if you executed an OLDER copy of SupermarketDb_Schema.sql
--
--  WHY: the first version of the schema script had no "USE [SupermarketDb]" header, so SQL Server
--  created its tables inside whatever database the SSMS toolbar pointed at (usually [master]).
--
--  HOW TO USE:
--    1. Check the toolbar: it must show the database you want to CLEAN (normally [master]).
--    2. Press F5.
--    3. Re-run database\SupermarketDb_Schema.sql afterwards.
--
--  It drops ONLY the 21 tables that the schema script creates, and nothing else. Safe to run twice.
--  Do NOT run it against SupermarketDb once the system is in production!
-- =============================================================================================

USE [master];
GO

-- Refuse to touch a database that is not master/model/msdb, so a wrong toolbar cannot lose data.
IF DB_NAME() NOT IN (N'master', N'model', N'msdb')
BEGIN
    RAISERROR(N'This script must be run against [master] (or another system database) - current database is %s. Nothing was changed.', 16, 1, DB_NAME());
    RETURN;
END
GO

SET NOCOUNT ON;

-- Reverse dependency order (children first) so no foreign key blocks the drop.
DROP TABLE IF EXISTS [dbo].[HeldCarts];
DROP TABLE IF EXISTS [dbo].[StockMovements];
DROP TABLE IF EXISTS [dbo].[Payments];
DROP TABLE IF EXISTS [dbo].[InvoiceItems];
DROP TABLE IF EXISTS [dbo].[ShiftReports];
DROP TABLE IF EXISTS [dbo].[Invoices];
DROP TABLE IF EXISTS [dbo].[PurchaseOrderItems];
DROP TABLE IF EXISTS [dbo].[StockCountItems];
DROP TABLE IF EXISTS [dbo].[PriceHistories];
DROP TABLE IF EXISTS [dbo].[UserPermissions];
DROP TABLE IF EXISTS [dbo].[Shifts];
DROP TABLE IF EXISTS [dbo].[PurchaseOrders];
DROP TABLE IF EXISTS [dbo].[Products];
DROP TABLE IF EXISTS [dbo].[Users];
DROP TABLE IF EXISTS [dbo].[Units];
DROP TABLE IF EXISTS [dbo].[Suppliers];
DROP TABLE IF EXISTS [dbo].[StoreSettings];
DROP TABLE IF EXISTS [dbo].[StockCounts];
DROP TABLE IF EXISTS [dbo].[Customers];
DROP TABLE IF EXISTS [dbo].[Categories];
DROP TABLE IF EXISTS [dbo].[__EFMigrationsHistory];
GO

PRINT N'Clean-up done: the 21 schema tables were removed from the current database.';
GO
