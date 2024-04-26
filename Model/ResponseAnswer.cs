using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class ResponseAnswer
    {
        public int Id { get; set; }
        public int ResponseDetailId { get; set; }

        [ForeignKey("ResponseDetailId")]
        public ResponseDetail? ResponseDetail { get; set; }
        public int AnswerId { get; set; }
    }
}
