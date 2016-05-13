﻿using System;

namespace Marten.Schema.Identity
{
    public class GuidIdGenerator : IIdGeneration<Guid>
    {
        private readonly Func<Guid> _guidmaker;

        public GuidIdGenerator(Func<Guid> guidmaker)
        {
            _guidmaker = guidmaker;
        }

        public Guid Assign(Guid existing, out bool assigned)
        {
            if (existing == Guid.Empty)
            {
                assigned = true;
                return _guidmaker();
            }

            assigned = false;
            return existing;
        }
    }
}