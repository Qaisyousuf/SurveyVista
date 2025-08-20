using Data;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;

namespace Services.Implemnetation
{
    public class QuestionnaireRepository : IQuestionnaireRepository
    {
        private readonly SurveyContext _context;

        public QuestionnaireRepository(SurveyContext Context)
        {
            _context = Context;
        }

        // EXISTING METHOD - Keep exactly as is
        public void Add(Questionnaire questionnaire)
        {
            _context.Questionnaires.Add(questionnaire);
        }

        // EXISTING METHOD - Keep exactly as is
        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        // EXISTING METHOD - Keep exactly as is
        public List<Questionnaire> GetAllQuestions()
        {
            return _context.Questionnaires.ToList();
        }

        // EXISTING METHOD - Keep exactly as is
        public Questionnaire GetQuesById(int? id)
        {
            return _context.Questionnaires.Find(id);
        }

        // UPDATE THIS METHOD - Add filter for active questions only
        public List<Questionnaire> GetQuestionnairesWithQuestion()
        {
            return _context.Questionnaires
                .AsNoTracking()
                .Include(x => x.Questions.Where(q => q.IsActive)) // Only get active questions
                .ThenInclude(x => x.Answers)
                .ToList();
        }

        // UPDATE THIS METHOD - Add filter for active questions only
        public Questionnaire GetQuestionnaireWithQuestionAndAnswer(int? id)
        {
            return _context.Questionnaires
                .Include(x => x.Questions.Where(q => q.IsActive)) // Only get active questions
                .ThenInclude(x => x.Answers)
                .FirstOrDefault(x => x.Id == id);
        }

        // EXISTING METHOD - Keep exactly as is
        public async Task Update(Questionnaire questionnaire)
        {
            _context.Questionnaires.Update(questionnaire);
            await _context.SaveChangesAsync();
        }

        // EXISTING METHOD - Keep exactly as is
        public async Task Delete(int? id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id), "ID cannot be null");
            }
            var questionnaire = GetQuesById(id);
            if (questionnaire == null)
            {
                throw new ArgumentException("Questionnaire not found", nameof(id));
            }
            _context.Questionnaires.Remove(questionnaire);
            await _context.SaveChangesAsync();
        }

        // ADD THESE NEW METHODS (for future status management):

        // Get questionnaires by status
        public List<Questionnaire> GetQuestionnairesByStatus(QuestionnaireStatus status)
        {
            return _context.Questionnaires
                .Where(q => q.Status == status)
                .Include(x => x.Questions.Where(q => q.IsActive))
                .ThenInclude(x => x.Answers)
                .ToList();
        }

        // Get all questionnaires with status info (for admin dashboard)
        public List<Questionnaire> GetAllQuestionnairesWithStatus()
        {
            return _context.Questionnaires
                .Include(x => x.Questions.Where(q => q.IsActive))
                .ThenInclude(x => x.Answers)
                .OrderByDescending(q => q.CreatedDate)
                .ToList();
        }

        // Check if questionnaire has responses (useful for status changes)
        public async Task<bool> HasResponses(int questionnaireId)
        {
            return await _context.Responses
                .AnyAsync(r => r.QuestionnaireId == questionnaireId);
        }

        // Update questionnaire status
        public async Task UpdateStatus(int questionnaireId, QuestionnaireStatus newStatus)
        {
            var questionnaire = await _context.Questionnaires.FindAsync(questionnaireId);
            if (questionnaire != null)
            {
                questionnaire.Status = newStatus;

                // Set appropriate timestamps
                switch (newStatus)
                {
                    case QuestionnaireStatus.Published:
                        if (questionnaire.PublishedDate == null)
                            questionnaire.PublishedDate = DateTime.UtcNow;
                        break;
                    case QuestionnaireStatus.Archived:
                        if (questionnaire.ArchivedDate == null)
                            questionnaire.ArchivedDate = DateTime.UtcNow;
                        break;
                    case QuestionnaireStatus.Draft:
                        questionnaire.PublishedDate = null;
                        questionnaire.ArchivedDate = null;
                        break;
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
