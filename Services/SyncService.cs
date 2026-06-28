using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using POS.Client.Data;
using POS.Client.Models;
using POS.Client.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Client.Services
{
    public class SyncService
    {
        private readonly ApiService _apiService;
        private readonly POSDbContext _db;

        public SyncService()
        {
            _apiService = new ApiService();
            _db = new POSDbContext();
        }

        public async Task SyncProductsAsync(int companyId)
        {
            var products = await _apiService.GetProductsForPOSAsync(companyId);

            // Clear old product data only (NOT inventory/materials — server-side only)
            _db.ProductAddonItems.RemoveRange(_db.ProductAddonItems);
            _db.ProductAddons.RemoveRange(_db.ProductAddons);
            _db.ProductVariants.RemoveRange(_db.ProductVariants);
            _db.ProductModifiers.RemoveRange(_db.ProductModifiers);
            _db.ModifierOptions.RemoveRange(_db.ModifierOptions);
            _db.ModifierGroups.RemoveRange(_db.ModifierGroups);
            _db.Products.RemoveRange(_db.Products);
            _db.SaveChanges();

            foreach (var p in products)
            {
                var product = new LocalProduct
                {
                    ServerId = p.Id,
                    companyId = companyId,
                    Name = p.Name,
                    BasePrice = p.BasePrice,
                    TaxRate = p.TaxRate,
                    IsActive = true,
                    LastUpdated = DateTime.Now
                };

                _db.Products.Add(product);
                _db.SaveChanges();

                // Variants
                foreach (var v in p.Variants ?? new List<VariantResponse>())
                {
                    _db.ProductVariants.Add(new LocalProductVariant
                    {
                        ServerId = v.Id,
                        ProductId = p.Id,
                        Name = v.Name,
                        PriceAdjustment = v.PriceAdjustment,
                        IsActive = true
                    });
                }

                // Modifiers
                foreach (var m in p.Modifiers ?? new List<ModifierResponse>())
                {
                    _db.ProductModifiers.Add(new LocalProductModifier
                    {
                        ServerId = m.Id,
                        ProductId = p.Id,
                        GroupId = m.Group?.Id ?? 0,
                        IsRequired = m.IsRequired
                    });

                    if (m.Group != null)
                    {
                        if (!_db.ModifierGroups.Any(g => g.ServerId == m.Group.Id))
                        {
                            var group = new LocalModifierGroup
                            {
                                ServerId = m.Group.Id,
                                companyId = companyId,
                                Name = m.Group.Name,
                                SelectionType = m.Group.SelectionType,
                                MinSelect = 0,
                                MaxSelect = 1,
                                IsActive = true
                            };
                            _db.ModifierGroups.Add(group);
                            _db.SaveChanges();

                            foreach (var o in m.Group.Options ?? new List<OptionResponse>())
                            {
                                _db.ModifierOptions.Add(new LocalModifierOption
                                {
                                    ServerId = o.Id,
                                    GroupId = m.Group.Id,
                                    Name = o.Name,
                                    PriceAdjustment = o.PriceAdjustment,
                                    IsActive = true
                                });
                            }
                        }
                    }
                }

                // ADDON
                foreach (var addon in p.Addons ?? new List<AddonResponse>())
                {
                    var localAddon = new LocalProductAddon
                    {
                        ServerId = addon.Id,
                        ProductId = p.Id,
                        Name = addon.Name,
                        MaxQuantity = addon.MaxQuantity,
                        SortOrder = addon.SortOrder,
                        IsActive = addon.IsActive,
                        CreatedAt = addon.CreatedAt
                    };
                    _db.ProductAddons.Add(localAddon);
                    _db.SaveChanges();

                    foreach (var item in addon.Items ?? new List<AddonItemResponse>())
                    {
                        _db.ProductAddonItems.Add(new LocalProductAddonItem
                        {
                            ServerId = item.Id,
                            AddonId = localAddon.ServerId,
                            AddonProductId = item.AddonProductId,
                            QuantityValue = item.QuantityValue,
                            SortOrder = item.SortOrder,
                            IsActive = item.IsActive
                        });
                    }
                }
            }

            _db.SaveChanges();
        }

        public List<LocalProduct> GetProducts()
        {
            using var db = new POSDbContext();
            return db.Products
                .AsNoTracking()
                .Where(p => p.IsActive)
                .Include(p => p.Addons)
                .ThenInclude(a => a.Items)
                .ToList();
        }

        public List<LocalProductVariant> GetVariants(int productId)
        {
            return _db.ProductVariants.Where(v => v.ProductId == productId && v.IsActive).ToList();
        }

        public List<LocalModifierGroup> GetModifierGroups(int productId)
        {
            var groupIds = _db.ProductModifiers
                .Where(m => m.ProductId == productId)
                .Select(m => m.GroupId)
                .ToList();

            return _db.ModifierGroups
                .Where(g => groupIds.Contains(g.ServerId))
                .ToList();
        }

        public List<LocalModifierOption> GetModifierOptions(int groupId)
        {
            return _db.ModifierOptions.Where(o => o.GroupId == groupId && o.IsActive).ToList();
        }

        public LocalModifierGroup GetModifierGroup(int groupId)
        {
            using var db = new POSDbContext();
            return db.ModifierGroups
                .Include(g => g.Options)
                .FirstOrDefault(g => g.ServerId == groupId);
        }

        public List<LocalProductAddon> GetAddons(int productId)
        {
            return _db.ProductAddons
                .Where(a => a.ProductId == productId && a.IsActive)
                .Include(a => a.Items)
                .OrderBy(a => a.SortOrder)
                .ToList();
        }

        public LocalProductAddon GetAddon(int addonId)
        {
            using var db = new POSDbContext();
            return db.ProductAddons
                .Include(a => a.Items)
                .FirstOrDefault(a => a.ServerId == addonId);
        }

        public List<LocalProductAddonItem> GetAddonItems(int addonId)
        {
            return _db.ProductAddonItems
                .Where(i => i.AddonId == addonId && i.IsActive)
                .OrderBy(i => i.SortOrder)
                .ToList();
        }

        public LocalProduct GetProductWithAddons(int productId)
        {
            using var db = new POSDbContext();
            return db.Products
                .Include(p => p.Addons)
                .ThenInclude(a => a.Items)
                .FirstOrDefault(p => p.ServerId == productId);
        }
    }
}