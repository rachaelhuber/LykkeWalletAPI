﻿using FluentValidation;
using Lykke.Service.CandlesHistory.Client.Models;
using LykkeApi2.Strings;
using System;
using LykkeApi2.Models.CandleSticks;

namespace LykkeApi2.Models.ValidationModels.CandleSticksRequestValidations
{
    public class CandleSticksRequestValidationModel : AbstractValidator<CandleSticksRequestModel>
    {
        public CandleSticksRequestValidationModel()
        {
            RuleFor(r=>r.AssetPairId).NotEmpty().WithMessage(Phrases.FieldShouldNotBeEmpty);

            CandlePriceType priceTypeParsed;
            
            RuleFor(r=> Enum.TryParse(r.PriceType.ToString(), out priceTypeParsed)).Equal(true).WithName("PriceType").WithMessage((request) => $"Unknown priceType {request.PriceType}");

            CandleTimeInterval timeIntervalParsed;
            RuleFor(r => Enum.TryParse(r.TimeInterval.ToString(), out timeIntervalParsed)).Equal(true).WithName("TimeInterval").WithMessage((request) => $"Unknown timeInterval {request.TimeInterval}");            
        }
    }
}
