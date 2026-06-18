using BiometricAttendanceSystem.Models;
using BiometricAttendanceSystem.Models.ViewModels;
using BiometricAttendanceSystem.Repositories;

namespace BiometricAttendanceSystem.Services
{
    public class TeacherService : ITeacherService
    {
        private readonly ITeacherRepository    _teacherRepo;
        private readonly IDepartmentRepository _deptRepo;

        public TeacherService(ITeacherRepository t, IDepartmentRepository d)
        { _teacherRepo = t; _deptRepo = d; }

        public Task<List<Teacher>> GetAllTeachersAsync() =>
            _teacherRepo.GetAllAsync();

        public Task<List<Department>> GetDepartmentsAsync() =>
        _deptRepo.GetAllAsync();
        public Task<Teacher?> GetTeacherByIdAsync(int id) =>
            _teacherRepo.GetByIdAsync(id);

        public Task<Teacher?> GetTeacherByIdIncludingInactiveAsync(int id) =>
            _teacherRepo.GetByIdIncludingInactiveAsync(id);

        public Task<List<Teacher>> GetFormerEmployeesAsync() =>
            _teacherRepo.GetFormerEmployeesAsync();
        public async Task<Teacher?> GetTeacherByDeviceCookieAsync(string deviceCookie)
        {
            var all = await _teacherRepo.GetAllAsync();
            return all.FirstOrDefault(t => t.RegisteredDeviceId == deviceCookie && t.IsActive);
        }
        public async Task<(bool, string)> RegisterTeacherAsync(Teacher teacher)
        {
            if (await _teacherRepo.NationalIdExistsAsync(teacher.NationalId))
                return (false, $"National ID '{teacher.NationalId}' is already registered.");
            teacher.DateRegistered = DateTime.Now;
            await _teacherRepo.AddAsync(teacher);
            return (true, $"{teacher.FullName} registered successfully.");
        }

        public async Task<(bool, string)> UpdateTeacherAsync(Teacher teacher)
        {
            if (await _teacherRepo.NationalIdExistsAsync(teacher.NationalId, teacher.Id))
                return (false, "National ID already used by another teacher.");
            await _teacherRepo.UpdateAsync(teacher);
            return (true, "Teacher updated successfully.");
        }

        public async Task<bool> DeactivateTeacherAsync(int id)
        {
            var t = await _teacherRepo.GetByIdAsync(id);
            if (t == null) return false;
            await _teacherRepo.DeactivateAsync(id);
            return true;
        }

        public async Task<TeacherSearchVM> SearchTeachersAsync(string query, string searchBy)
        {
            var results = string.IsNullOrWhiteSpace(query)
                ? await _teacherRepo.GetAllAsync()
                : await _teacherRepo.SearchAsync(query, searchBy);
            return new TeacherSearchVM { Query = query, SearchBy = searchBy, Results = results };
        }
    }
}
