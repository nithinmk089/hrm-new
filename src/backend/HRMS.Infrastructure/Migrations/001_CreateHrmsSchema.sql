/*
    Migration: 001_CreateHrmsSchema
    Purpose: Create the application-owned schema for HRMS tables.
*/

IF SCHEMA_ID(N'hrms') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [hrms] AUTHORIZATION [dbo];');
END;
