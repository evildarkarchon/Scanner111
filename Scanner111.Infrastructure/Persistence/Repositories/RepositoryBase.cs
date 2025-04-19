﻿using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Scanner111.Core.Interfaces.Repositories;

namespace Scanner111.Infrastructure.Persistence.Repositories;

public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext DbContext;
    
    protected RepositoryBase(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }
    
    public async Task<T?> GetByIdAsync(string id)
    {
        return await DbContext.Set<T>().FindAsync(id);
    }
    
    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await DbContext.Set<T>().ToListAsync();
    }
    
    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await DbContext.Set<T>().Where(predicate).ToListAsync();
    }
    
    public async Task AddAsync(T entity)
    {
        await DbContext.Set<T>().AddAsync(entity);
        await DbContext.SaveChangesAsync();
    }
    
    public async Task UpdateAsync(T entity)
    {
        DbContext.Entry(entity).State = EntityState.Modified;
        await DbContext.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(string id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            DbContext.Set<T>().Remove(entity);
            await DbContext.SaveChangesAsync();
        }
    }
    
    public async Task<bool> ExistsAsync(string id)
    {
        return await GetByIdAsync(id) != null;
    }
}