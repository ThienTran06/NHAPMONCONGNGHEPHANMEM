-- DormitoryManagement database bootstrap.
-- Prefer EF Core migrations for schema creation; use this script only for local setup.
IF DB_ID(N'DormitoryManagementDb') IS NULL
BEGIN
    CREATE DATABASE DormitoryManagementDb;
END
GO
