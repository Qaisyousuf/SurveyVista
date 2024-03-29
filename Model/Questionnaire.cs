﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Questionnaire
    {
        public Questionnaire()
        {
            Questions = new List<Question>();
        }
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<Question>? Questions { get; set; }
    }
}
