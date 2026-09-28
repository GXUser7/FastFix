import subprocess, json
M = r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
def q(sql):
    out = subprocess.run([M, "-uroot", "-P3307", "-h127.0.0.1", "--default-character-set=utf8mb4", "-N", "-B", "-e", sql],
                         capture_output=True, check=True).stdout.decode("utf-8")
    return [l.split("\t") for l in out.splitlines() if l]
db = "servicedesk"
res = {"tables": [], "fks": [], "checks": [], "indexes": []}
for name, comment in q(f"SELECT table_name, table_comment FROM information_schema.tables WHERE table_schema='{db}' AND table_type='BASE TABLE' ORDER BY create_time, table_name"):
    cols = q(f"SELECT column_name, column_type, is_nullable, column_key, IFNULL(column_default,'NONE'), extra, column_comment FROM information_schema.columns WHERE table_schema='{db}' AND table_name='{name}' ORDER BY ordinal_position")
    res["tables"].append({"name": name, "comment": comment, "columns": [dict(zip(["name","type","nullable","key","default","extra","comment"], c)) for c in cols]})
res["fks"] = [dict(zip(["name","table","column","ref_table","ref_column","on_delete"], r)) for r in q(f"""
SELECT k.constraint_name, k.table_name, k.column_name, k.referenced_table_name, k.referenced_column_name, r.delete_rule
FROM information_schema.key_column_usage k JOIN information_schema.referential_constraints r
  ON r.constraint_name=k.constraint_name AND r.constraint_schema=k.constraint_schema
WHERE k.table_schema='{db}' AND k.referenced_table_name IS NOT NULL ORDER BY k.table_name, k.constraint_name""")]
res["checks"] = [dict(zip(["name","table","clause"], r)) for r in q(f"""
SELECT c.constraint_name, t.table_name, c.check_clause FROM information_schema.check_constraints c
JOIN information_schema.table_constraints t ON t.constraint_name=c.constraint_name AND t.constraint_schema=c.constraint_schema
WHERE c.constraint_schema='{db}' ORDER BY t.table_name""")]
res["indexes"] = [dict(zip(["table","name","unique","columns"], r)) for r in q(f"""
SELECT table_name, index_name, IF(non_unique=0,'да','нет'), GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ', ')
FROM information_schema.statistics WHERE table_schema='{db}' GROUP BY table_name, index_name, non_unique ORDER BY table_name, index_name""")]
json.dump(res, open("schema.json","w",encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(res["tables"]), "tables", len(res["fks"]), "fks", len(res["checks"]), "checks", len(res["indexes"]), "indexes")
