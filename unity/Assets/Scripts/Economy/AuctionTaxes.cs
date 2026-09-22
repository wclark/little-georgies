using System;
using System.Collections.Generic;

namespace LittleGeorgies.Economy
{
    [Serializable] public sealed class AuctionTaxPolicy
    {
        public int ValuePercent;
        public void Validate()
        {
            if (ValuePercent < 0 || ValuePercent > 100)
                throw new ArgumentException("Tax rate must be a whole percentage from 0 to 100.");
        }
    }
    [Serializable] public sealed class GeorgieTaxResult
    {
        public string GeorgieId;
        public long Value;
        public long BidHundredths;
        public long RentHundredths;
        public long TaxTenThousandths;
        public long SurplusTenThousandths;
    }
    [Serializable] public sealed class AuctionTaxReport
    {
        public int ValuePercent;
        public long TotalValue;
        public long TotalBidHundredths;
        public long TotalRentHundredths;
        public long TotalTaxTenThousandths;
        public long TotalSurplusTenThousandths;
        public List<GeorgieTaxResult> Georgies = new List<GeorgieTaxResult>();
    }
    public static class AuctionTaxes
    {
        // Whole apples, nearest integer with half an apple rounded up.
        public static long TaxOnValue(long value, int percent) => (value * percent + 50) / 100;

        public static AuctionTaxReport Calculate(AuctionResult auction, AuctionTaxPolicy policy)
        {
            if (auction == null || policy == null) throw new ArgumentException("An auction result and tax policy are required.");
            policy.Validate();
            if (auction.ValueTaxPercent != policy.ValuePercent)
                throw new ArgumentException("Tax rate changed: recompute the auction on the new bids.");
            if (auction.AmountScale != 1)
                throw new ArgumentException("Recompute the auction with whole-apple bids before reporting taxes.");
            var report = new AuctionTaxReport { ValuePercent = policy.ValuePercent };
            foreach (var person in auction.Georgies)
            {
                long value = person.Won ? person.Award.Value : 0;
                long tax = TaxOnValue(value, policy.ValuePercent);
                long bidHundredths = person.Won ? person.Award.Bid * 100 : 0;
                long rentHundredths = person.Payment * 100;
                if (bidHundredths != (value - tax) * 100)
                    throw new ArgumentException("The winning bid must equal value minus rounded tax.");
                var row = new GeorgieTaxResult { GeorgieId = person.GeorgieId, Value = value,
                    BidHundredths = bidHundredths, RentHundredths = rentHundredths,
                    TaxTenThousandths = tax * 10000 };
                // Preserve the saved report's fixed-point units; surplus is after tax and rent.
                row.SurplusTenThousandths = (bidHundredths - rentHundredths) * 100;
                report.Georgies.Add(row);
                report.TotalValue += row.Value; report.TotalBidHundredths += row.BidHundredths;
                report.TotalRentHundredths += row.RentHundredths;
                report.TotalTaxTenThousandths += row.TaxTenThousandths;
                report.TotalSurplusTenThousandths += row.SurplusTenThousandths;
            }
            return report;
        }
    }
}
