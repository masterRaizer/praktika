SET NOCOUNT ON;
SELECT (
SELECT t.name AS tbl, c.column_id AS ord, c.name AS col,
       LOWER(ty.name) + CASE WHEN ty.name IN ('nvarchar','varchar','char','nchar') THEN '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(CASE WHEN ty.name LIKE 'n%' THEN c.max_length/2 ELSE c.max_length END AS varchar(10)) END + ')'
                             WHEN ty.name IN ('decimal','numeric') THEN '(' + CAST(c.precision AS varchar(5)) + ',' + CAST(c.scale AS varchar(5)) + ')'
                             WHEN ty.name IN ('datetime2','time') THEN '(' + CAST(c.scale AS varchar(5)) + ')' ELSE '' END AS [type],
       CAST(c.is_nullable AS bit) AS nullable,
       CASE WHEN EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                         WHERE i.is_primary_key = 1 AND ic.object_id = c.object_id AND ic.column_id = c.column_id) THEN 1 ELSE 0 END AS pk,
       (SELECT TOP 1 OBJECT_NAME(fkc.referenced_object_id) FROM sys.foreign_key_columns fkc
         WHERE fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id) AS fk,
       CASE WHEN EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                         WHERE i.is_unique_constraint = 1 AND ic.object_id = c.object_id AND ic.column_id = c.column_id) THEN 1 ELSE 0 END AS uq
FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
ORDER BY t.name, c.column_id
FOR JSON PATH) AS j;
