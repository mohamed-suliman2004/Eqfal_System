using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Models;

namespace Eqfal.API.Data
{
    public static class SubscriptionSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // 1. إعدادات النظام وفترة السماح
            if (!await context.SubscriptionSettings.AnyAsync())
            {
                context.SubscriptionSettings.Add(new SubscriptionSettings
                {
                    GracePeriodDays = 3,
                    UpdatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // 2. خطة التجربة المجانية (7 أيام)
            var trialPlan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.IsTrialPlan);
            if (trialPlan == null)
            {
                trialPlan = new SubscriptionPlan
                {
                    Name = "Free Trial",
                    NameAr = "تجربة مجانية",
                    DescriptionAr = "تجربة مفتوحة بالكامل لمدة 7 أيام لجميع ميزات النظام",
                    IsTrialPlan = true,
                    TrialDurationDays = 7,
                    IsPopular = false,
                    IsActive = true,
                    SortOrder = 0,
                    ThemeKey = "green",
                    CreatedAt = DateTime.UtcNow
                };

                trialPlan.Features.Add(new SubscriptionPlanFeature { Text = "مراقبة كاملة لجميع المحادثات بدون حدود", SortOrder = 1 });
                trialPlan.Features.Add(new SubscriptionPlanFeature { Text = "تحليل وتوثيق جميع العمليات المالية تلقائياً", SortOrder = 2 });
                trialPlan.Features.Add(new SubscriptionPlanFeature { Text = "تصدير كشوفات الحساب وتقارير PDF و Excel", SortOrder = 3 });

                context.SubscriptionPlans.Add(trialPlan);
                await context.SaveChangesAsync();
            }

            // 3. الخطة الشاملة الأساسية بالـ 4 دورات
            var proPlan = await context.SubscriptionPlans
                .Include(p => p.Prices)
                .FirstOrDefaultAsync(p => !p.IsTrialPlan);

            if (proPlan == null)
            {
                proPlan = new SubscriptionPlan
                {
                    Name = "Eqfal Pro",
                    NameAr = "الخطة الاحترافية الشاملة",
                    DescriptionAr = "وصول كامل ومفتوح لجميع ميزات تطبيق إقفال بدون أي قيود",
                    IsTrialPlan = false,
                    IsPopular = true,
                    IsActive = true,
                    SortOrder = 1,
                    ThemeKey = "blue",
                    CreatedAt = DateTime.UtcNow
                };

                proPlan.Features.Add(new SubscriptionPlanFeature { Text = "مراقبة غير محدودة للمحادثات والمجموعات", SortOrder = 1 });
                proPlan.Features.Add(new SubscriptionPlanFeature { Text = "معالجة عدد غير محدود من العمليات المالية", SortOrder = 2 });
                proPlan.Features.Add(new SubscriptionPlanFeature { Text = "تصدير فوري لكشوفات الحساب Excel & PDF", SortOrder = 3 });
                proPlan.Features.Add(new SubscriptionPlanFeature { Text = "دعم فني مباشر وتحديثات فورية مستمرة", SortOrder = 4 });

                // أسعار الدورات الأربعة:
                // 1. شهر: 50 د.ل (30 يوماً)
                proPlan.Prices.Add(new SubscriptionPlanPrice
                {
                    BillingCycle = BillingCycle.Monthly,
                    Price = 50.00m,
                    DurationDays = 30,
                    IsActive = true
                });

                // 2. 3 شهور: 135 د.ل (90 يوماً - توفير 10%)
                proPlan.Prices.Add(new SubscriptionPlanPrice
                {
                    BillingCycle = BillingCycle.Quarterly,
                    Price = 135.00m,
                    DurationDays = 90,
                    IsActive = true
                });

                // 3. 6 شهور: 240 د.ل (180 يوماً - توفير 20%)
                proPlan.Prices.Add(new SubscriptionPlanPrice
                {
                    BillingCycle = BillingCycle.SemiAnnual,
                    Price = 240.00m,
                    DurationDays = 180,
                    IsActive = true
                });

                // 4. سنة: 420 د.ل (365 يوماً - توفير 30%)
                proPlan.Prices.Add(new SubscriptionPlanPrice
                {
                    BillingCycle = BillingCycle.Yearly,
                    Price = 420.00m,
                    DurationDays = 365,
                    IsActive = true
                });

                context.SubscriptionPlans.Add(proPlan);
                await context.SaveChangesAsync();
            }

            // 4. تصنيف مسوقين افتراضي
            if (!await context.MarketerCategories.AnyAsync())
            {
                context.MarketerCategories.Add(new MarketerCategory
                {
                    Name = "مسوق رقمي",
                    Description = "المسوقون عبر وسائل التواصل الاجتماعي والإنترنت",
                    DefaultCommissionType = DiscountType.Percentage,
                    DefaultCommissionValue = 10.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // 5. منح اشتراك تجريبي نشط (7 أيام) لكل المستخدمين الحاليين الذين لا يملكون اشتراكاً
            var usersWithoutSub = await context.Users
                .Where(u => !context.Subscriptions.Any(s => s.UserId == u.Id))
                .ToListAsync();

            if (usersWithoutSub.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var user in usersWithoutSub)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        UserId = user.Id,
                        PlanId = trialPlan.Id,
                        PlanType = trialPlan.NameAr,
                        PriceSnapshot = 0,
                        IsTrial = true,
                        StartedAt = now,
                        ExpiresAt = now.AddDays(trialPlan.TrialDurationDays ?? 7),
                        CreatedAt = now
                    });
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
