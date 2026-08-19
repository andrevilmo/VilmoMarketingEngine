export const COLUMN_TITLES = {
  review: 'Review',
  inProgress: 'In Progress',
  done: 'Done',
};

export const initialTasks = {
  review: [
    {
      id: '1',
      title: 'Add authentication',
      description: 'Implement user login and registration flow',
      priority: 'high',
      dueDate: 'Dec 15, 2025',
    },
    {
      id: '2',
      title: 'Create API endpoints',
      description: 'Build REST API for task management',
      priority: 'medium',
      dueDate: 'Dec 18, 2025',
    },
    {
      id: '3',
      title: 'Write documentation',
      description: 'Document API endpoints and usage',
      priority: 'low',
      dueDate: 'Dec 22, 2025',
    },
    {
      id: '8',
      title: 'Code review: payment module',
      description: 'Review PR for payment integration',
      priority: 'high',
      dueDate: 'Dec 16, 2025',
    },
    {
      id: '9',
      title: 'Security audit fixes',
      description: 'Address vulnerabilities from security scan',
      priority: 'high',
      dueDate: 'Dec 14, 2025',
    },
    {
      id: '10',
      title: 'Performance optimization',
      description: 'Review lazy loading implementation',
      priority: 'medium',
      dueDate: 'Dec 20, 2025',
    },
  ],

  inProgress: [
    {
      id: '4',
      title: 'Design system updates',
      description: 'Update UI components to match new design',
      priority: 'high',
      dueDate: 'Dec 12, 2025',
    },
    {
      id: '5',
      title: 'Implement dark mode',
      description: 'Add dark theme support across the app',
      priority: 'medium',
      dueDate: 'Dec 14, 2025',
    },
    {
      id: '11',
      title: 'User profile page',
      description: 'Build user settings and profile management',
      priority: 'medium',
      dueDate: 'Dec 17, 2025',
    },
    {
      id: '12',
      title: 'Email notifications',
      description: 'Implement email alerts for task updates',
      priority: 'low',
      dueDate: 'Dec 19, 2025',
    },
    {
      id: '13',
      title: 'Dashboard analytics',
      description: 'Add charts and statistics to dashboard',
      priority: 'high',
      dueDate: 'Dec 13, 2025',
    },
    {
      id: '14',
      title: 'Mobile responsive layout',
      description: 'Fix responsive issues on mobile devices',
      priority: 'medium',
      dueDate: 'Dec 16, 2025',
    },
  ],

  done: [
    {
      id: '6',
      title: 'Setup project',
      description: 'Initialize project structure and dependencies',
      priority: 'high',
      dueDate: 'Dec 5, 2025',
    },
    {
      id: '7',
      title: 'Initial commit',
      description: 'First commit with basic configuration',
      priority: 'low',
      dueDate: 'Dec 3, 2025',
    },
    {
      id: '15',
      title: 'Database schema design',
      description: 'Create database models and migrations',
      priority: 'high',
      dueDate: 'Dec 4, 2025',
    },
    {
      id: '16',
      title: 'CI/CD pipeline',
      description: 'Setup automated testing and deployment',
      priority: 'medium',
      dueDate: 'Dec 6, 2025',
    },
    {
      id: '17',
      title: 'Landing page design',
      description: 'Create hero section and marketing content',
      priority: 'medium',
      dueDate: 'Dec 7, 2025',
    },
    {
      id: '18',
      title: 'Unit tests setup',
      description: 'Configure Jest and write initial tests',
      priority: 'low',
      dueDate: 'Dec 8, 2025',
    },
    {
      id: '19',
      title: 'Environment configuration',
      description: 'Setup dev, staging, and production envs',
      priority: 'high',
      dueDate: 'Dec 5, 2025',
    },
  ],
};
