-- Update @BackupPath for your environment before running.
DECLARE @BackupPath nvarchar(4000) = N'C:\Backups\DormitoryManagementDb.bak';

BACKUP DATABASE DormitoryManagementDb
TO DISK = @BackupPath
WITH FORMAT, INIT, NAME = N'DormitoryManagementDb Full Backup';
GO
