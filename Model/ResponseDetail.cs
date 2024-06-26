﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class ResponseDetail
    {
        public int Id { get; set; }

        
        public int ResponseId { get; set; }

        [ForeignKey("ResponseId")]
        public Response? Response { get; set; }
        public int QuestionId { get; set; }

        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }
        public QuestionType QuestionType { get; set; }
        public string? TextResponse { get; set; }
        public List<ResponseAnswer> ResponseAnswers { get; set; } = new List<ResponseAnswer>();
    }
}
