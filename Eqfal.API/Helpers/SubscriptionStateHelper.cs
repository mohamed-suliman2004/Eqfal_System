using System;
using Eqfal.API.Models;

namespace Eqfal.API.Helpers
{
    public enum PlanChangeKind
    {
        Renewal = 1,   // تجديد نفس الخطة ونفس الدورة
        Change = 2,    // تغيير الخطة أو الدورة أثناء سريان الاشتراك (يتطلب تأكيد D6)
        NewStart = 3   // بداية جديدة (تجربة منتهية، سماح، أو لا يوجد اشتراك)
    }

    public static class SubscriptionStateHelper
    {
        /// <summary>
        /// حساب حالة الاشتراك لحظياً
        /// </summary>
        public static string ComputeStatus(DateTime expiresAt, bool isTrial, DateTime now, int gracePeriodDays = 3)
        {
            if (now <= expiresAt)
                return isTrial ? "Trial" : "Active";

            if (gracePeriodDays > 0 && now <= expiresAt.AddDays(gracePeriodDays))
                return "Grace";

            return "Expired";
        }

        /// <summary>
        /// تاريخ انتهاء فترة السماح
        /// </summary>
        public static DateTime? ComputeGraceUntil(DateTime expiresAt, int gracePeriodDays = 3)
        {
            return gracePeriodDays > 0 ? expiresAt.AddDays(gracePeriodDays) : null;
        }

        /// <summary>
        /// الأيام المتبقية
        /// </summary>
        public static int ComputeDaysRemaining(DateTime expiresAt, DateTime now)
        {
            if (expiresAt <= now) return 0;
            return (int)Math.Ceiling((expiresAt - now).TotalDays);
        }

        /// <summary>
        /// هل الاشتراك سارٍ ولم ينتهِ وغير تجريبي
        /// </summary>
        public static bool IsOngoing(Subscription? sub, DateTime now)
        {
            return sub != null && now <= sub.ExpiresAt && !sub.IsTrial;
        }

        /// <summary>
        /// تحديد نوع الحركة (Renewal / Change / NewStart) - قاعدة D6
        /// </summary>
        public static PlanChangeKind DetermineKind(Subscription? sub, Guid newPlanPriceId, DateTime now)
        {
            if (!IsOngoing(sub, now))
                return PlanChangeKind.NewStart;

            if (sub!.PlanPriceId == newPlanPriceId)
                return PlanChangeKind.Renewal;

            return PlanChangeKind.Change;
        }

        /// <summary>
        /// حساب التاريخ الأساسي لبدء الاحتساب
        /// </summary>
        public static DateTime ComputeBaseDate(Subscription? sub, PlanChangeKind kind, DateTime now)
        {
            return (kind == PlanChangeKind.Renewal && sub != null) ? sub.ExpiresAt : now;
        }

        /// <summary>
        /// حساب نسبة التوفير لدورة معينة مقارنة بالسعر الشهري
        /// </summary>
        public static int? CalculateSavingPercent(decimal? monthlyPrice, decimal? cyclePrice, BillingCycle cycle)
        {
            if (monthlyPrice == null || monthlyPrice <= 0 || cyclePrice == null || cyclePrice <= 0)
                return null;

            int monthsMultiplier = cycle switch
            {
                BillingCycle.Quarterly => 3,
                BillingCycle.SemiAnnual => 6,
                BillingCycle.Yearly => 12,
                _ => 1
            };

            if (monthsMultiplier == 1) return null;

            decimal fullPeriodMonthlyPrice = monthlyPrice.Value * monthsMultiplier;
            decimal savingRatio = 1 - (cyclePrice.Value / fullPeriodMonthlyPrice);
            int percent = (int)Math.Round(savingRatio * 100, MidpointRounding.AwayFromZero);

            return percent > 0 ? percent : null;
        }
    }
}
