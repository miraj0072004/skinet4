using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.RequestHelpers
{
    public class Pagination<T>(int pageIndex, int pageCount, int count, IReadOnlyList<T> data)
    {
        
        public int PageIndex { get; set; } = pageIndex;
        public int PageSize { get; set;} = pageCount;
        public int Count { get; set;} = count;
        public IReadOnlyList<T> Data { get; set; } = data;
    }
}