﻿using Web.ViewModel.QuestionVM;

namespace Web.ViewModel.QuestionnaireVM
{
    public class ResponseQuestionnaireViewModel
    {
       
            public int Id { get; set; }  // Questionnaire ID
            public string? Title { get; set; }  // Title of the questionnaire
            public string? Description { get; set; }  // Description of the questionnaire

            // Collection of questions
            public List<ResponseQuestionViewModel> Questions { get; set; } = new List<ResponseQuestionViewModel>();
       

    }
}
