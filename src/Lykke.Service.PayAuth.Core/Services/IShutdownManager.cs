﻿using System.Threading.Tasks;

namespace Lykke.Service.PayAuth.Core.Services
{
    public interface IShutdownManager
    {
        Task StopAsync();
    }
}