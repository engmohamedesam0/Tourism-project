"""Removes 3,500 of the generated tourist accounts (Tourist Ids 21..3520),
keeping the 20 original + 520 generated (Ids 3521..4040). Deletes in FK-safe
order: user roles -> tourists -> users. Verifies nothing else references them."""
import psycopg2

conn = psycopg2.connect(host='localhost', port=5432, dbname='Tourist_PostGIS_DB_MVC',
                        user='postgres', password='admin')
conn.autocommit = False
cur = conn.cursor()

# the generated accounts to remove: Tourists with Id BETWEEN 21 AND 3520
cur.execute('SELECT "Id", "ApplicationUserId" FROM "Tourists" WHERE "Id" BETWEEN 21 AND 3520')
to_remove = cur.fetchall()
print('accounts to remove:', len(to_remove))
user_ids = [r[1] for r in to_remove]

# delete in FK-safe order — if any table still references these users, the
# DELETE will fail here with the constraint name (nothing is committed then)
cur.execute('DELETE FROM "AspNetUserRoles" WHERE "UserId" = ANY(%s)', (user_ids,))
print('roles deleted:', cur.rowcount)
cur.execute('DELETE FROM "Tourists" WHERE "Id" BETWEEN 21 AND 3520')
print('tourists deleted:', cur.rowcount)
cur.execute('DELETE FROM "AspNetUsers" WHERE "Id" = ANY(%s)', (user_ids,))
print('users deleted:', cur.rowcount)

conn.commit()

cur.execute('SELECT COUNT(*) FROM "Tourists"')
print('Tourists now:', cur.fetchone()[0])
cur.execute('SELECT COUNT(*) FROM "AspNetUsers"')
print('AspNetUsers now:', cur.fetchone()[0])
cur.execute('SELECT COUNT(*) FROM "AspNetUserRoles"')
print('AspNetUserRoles now:', cur.fetchone()[0])
cur.execute("""SELECT COUNT(DISTINCT u."Nationality") FROM "Tourists" t
               JOIN "AspNetUsers" u ON t."ApplicationUserId" = u."Id" """)
print('distinct nationalities now:', cur.fetchone()[0])

conn.close()
