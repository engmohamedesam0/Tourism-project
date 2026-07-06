using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;

namespace Tourist_Project_MVC.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly TouristContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(TouristContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public IEnumerable<T> GetAll() => _dbSet.ToList();

        public T? GetById(int id) => _dbSet.Find(id);

        public void Add(T entity) => _dbSet.Add(entity);

        public void Update(T entity) => _dbSet.Update(entity);

        public void Delete(int id)
        {
            var entity = _dbSet.Find(id);
            if (entity != null) _dbSet.Remove(entity);
        }

        public void Save() => _context.SaveChanges();
    }
}